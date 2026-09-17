import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Notifications.Inbox.View";
const mark = "Permissions.Notifications.Inbox.MarkRead";
const notification = { id: "n1", title: "Test notification", type: "test", source: "System", readAtUtc: null, createdAtUtc: "2026-09-17T00:00:00Z", metadataJson: "{}" };

test("no View denies direct URL and both notification surfaces make no requests", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [mark] });
  await installAdminShellMocks(page, [mark]);
  const calls: string[] = [];
  page.on("request", request => { if (request.url().includes("/api/v1/notifications")) calls.push(request.url()); });
  await page.goto("/notifications");
  await expect(page.getByRole("main")).toContainText(/permission|access denied/i);
  await expect(page.getByRole("button", { name: /notifications/i })).toHaveCount(0);
  expect(calls).toEqual([]);
});

test("View-only sees inbox and preview but neither exposes writes", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [view] });
  await installAdminShellMocks(page, [view]);
  await page.route("**/api/v1/notifications/?*", route => route.fulfill({ json: [notification] }));
  await page.route("**/api/v1/notifications/unread-count", route => route.fulfill({ json: 1 }));
  await page.goto("/notifications");
  await expect(page.getByRole("main").getByText(notification.title)).toBeVisible();
  await page.getByRole("button", { name: "1 unread notifications" }).click();
  const preview = page.getByRole("region", { name: "Notifications", exact: true });
  await expect(preview.getByText(notification.title)).toBeVisible();
  await expect(page.getByRole("button", { name: /mark.*read/i })).toHaveCount(0);
});

test("preview failure does not claim inbox zero and can retry", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [view] });
  await installAdminShellMocks(page, [view]);
  let fail = true;
  await page.route("**/api/v1/notifications/?*", route => route.fulfill(fail ? { status: 403 } : { json: [notification] }));
  await page.goto("/settings/profile");
  await page.getByRole("button", { name: "Notifications", exact: true }).click();
  const preview = page.getByRole("region", { name: "Notifications", exact: true });
  await expect(preview.getByRole("alert")).toBeVisible();
  await expect(preview.getByText("You're all caught up.")).toHaveCount(0);
  fail = false;
  await preview.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(preview.getByText(notification.title)).toBeVisible();
});

for (const surface of ["inbox", "preview"]) {
  test(`${surface} writes require permissions, retry failure and lock both surfaces`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await seedAuthedSession(page, { ...TEST_USER, permissions: [view, mark] });
    await installAdminShellMocks(page, [view, mark]);
    let items = [notification, { ...notification, id: "n2", title: "Second notification" }];
    let release: (() => void) | undefined;
    const writes: string[] = [];
    await page.route("**/api/v1/notifications/**", async route => {
      const request = route.request();
      expect(request.headers().tenant).toBe("root");
      if (request.method() === "GET") {
        await route.fulfill({ json: request.url().endsWith("unread-count") ? items.length : items });
        return;
      }
      writes.push(new URL(request.url()).pathname);
      if (writes.length === 1) {
        await new Promise<void>(resolve => { release = resolve; });
        await route.fulfill({ status: 403, json: { detail: "Mark denied" } });
      } else {
        items = request.url().endsWith("read-all") ? [] : items.filter(item => item.id !== "n1");
        await route.fulfill(request.url().endsWith("read-all") ? { json: { updated: 1 } } : { status: 204 });
      }
    });
    await page.goto("/notifications");
    const main = page.getByRole("main");
    await expect(main.getByText(notification.title)).toBeVisible();
    if (surface === "preview") await page.getByRole("button", { name: "2 unread notifications" }).click();
    const scope = surface === "preview" ? page.getByRole("region", { name: "Notifications", exact: true }) : main;
    const single = scope.getByRole("button", { name: surface === "preview" ? "Mark as read" : "Mark read", exact: true }).first();
    await single.click();
    await expect.poll(() => !!release).toBe(true);
    await expect(single).toBeDisabled();
    await expect(main.getByRole("button", { name: "Mark all read", exact: true })).toBeDisabled();
    release?.();
    await expect(single).toBeEnabled();
    await single.click();
    await expect(scope.getByText(notification.title)).toHaveCount(0);
    await scope.getByRole("button", { name: "Mark all read", exact: true }).click();
    await expect(scope.getByText("Second notification")).toHaveCount(0);
    expect(writes).toEqual(["/api/v1/notifications/n1/read", "/api/v1/notifications/n1/read", "/api/v1/notifications/read-all"]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
