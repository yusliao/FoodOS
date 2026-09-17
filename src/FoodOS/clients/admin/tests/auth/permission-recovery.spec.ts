import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} direct URL permission failure retries without mounting business queries`, async ({ page }) => {
    await seedAuthedSession(page, TEST_USER);
    await installAdminShellMocks(page);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let phase = "initial";
    let release: (() => void) | undefined;
    await page.route("**/api/v1/identity/permissions", async route => {
      if (phase === "pending") await new Promise<void>(resolve => { release = resolve; });
      await route.fulfill(phase === "success" ? { json: [] } : { status: 403, json: {} });
    });
    const business: string[] = [];
    page.on("request", request => { if (request.url().includes("/api/v1/notifications")) business.push(request.url()); });
    await page.goto("/notifications");
    await expect(page.getByRole("alert")).toContainText(messages.workbench.permissionsFailed);
    await expect(page.getByRole("main")).toHaveCount(0);
    phase = "pending";
    const retry = page.getByRole("button", { name: messages.workbench.retry, exact: true });
    await retry.click();
    await expect.poll(() => !!release).toBe(true);
    await expect(retry).toBeDisabled();
    phase = "failed";
    release?.();
    await expect(retry).toBeEnabled();
    await expect(page.getByRole("alert")).toContainText(messages.workbench.permissionsFailed);
    phase = "success";
    await retry.click();
    await expect(page.getByRole("main")).toContainText(messages.common.forbiddenTitle);
    await expect(page).toHaveURL(/\/notifications$/);
    expect(business).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("late permission retry cannot repopulate permissions after sign out", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installAdminShellMocks(page);
  let retrying = false;
  let release: (() => void) | undefined;
  await page.route("**/api/v1/identity/permissions", async route => {
    if (!retrying) { await route.fulfill({ status: 403, json: {} }); return; }
    await new Promise<void>(resolve => { release = resolve; });
    await route.fulfill({ json: ["Permissions.Users.View"] });
  });
  await page.goto("/users");
  await expect(page.getByRole("alert")).toBeVisible();
  retrying = true;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect.poll(() => !!release).toBe(true);
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  const response = page.waitForResponse("**/api/v1/identity/permissions");
  release?.();
  await response;
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem("fsh.admin.permissions"))).toBeNull();
});
