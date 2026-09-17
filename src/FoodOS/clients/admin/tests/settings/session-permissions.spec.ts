import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Sessions.View";
const revoke = "Permissions.Sessions.Revoke";
const current = { id: "11111111-1111-4111-8111-111111111111", browser: "Chrome", operatingSystem: "Windows", isActive: true, isCurrentSession: true, createdAt: "2026-09-17", lastActivityAt: "2026-09-17", expiresAt: "2027-01-01" };
const other = { ...current, id: "22222222-2222-4222-8222-222222222222", browser: "Safari", isCurrentSession: false };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
}

test("no View denies direct URL and hides session navigation without requests", async ({ page }) => {
  await setup(page, [revoke]);
  let calls = 0;
  page.on("request", request => { if (request.url().includes("/api/v1/identity/sessions")) calls++; });
  await page.goto("/settings/sessions");
  await expect(page.getByText(view, { exact: true })).toBeVisible();
  await expect(page.locator('a[href="/settings/sessions"]')).toHaveCount(0);
  expect(calls).toBe(0);
});

test("View-only retries a 403 but has no write controls", async ({ page }) => {
  await setup(page, [view]);
  let denied = true;
  await page.route("**/api/v1/identity/sessions/me", route => route.fulfill(denied ? { status: 403, json: { detail: "Sessions unavailable" } } : { json: [current, other] }));
  await page.goto("/settings/sessions");
  await expect(page.getByText("Sessions unavailable", { exact: true })).toBeVisible();
  denied = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("This device", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Revoke", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Sign out everywhere else", exact: true })).toHaveCount(0);
});

test("unknown current session blocks bulk revoke", async ({ page }) => {
  await setup(page, [view, revoke]);
  await page.route("**/api/v1/identity/sessions/me", route => route.fulfill({ json: [other] }));
  await page.goto("/settings/sessions");
  await expect(page.getByRole("button", { name: "Sign out everywhere else", exact: true })).toBeDisabled();
  await expect(page.getByText(/The server has not identified the current session/)).toBeVisible();
});

test("bulk revoke excludes current session, blocks overlapping writes and retries failure", async ({ page }) => {
  await setup(page, [view, revoke]);
  let done = false;
  let calls = 0;
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/identity/sessions/me", route => route.fulfill({ json: done ? [current] : [current, other] }));
  await page.route("**/api/v1/identity/sessions/revoke-all", async route => {
    calls++;
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ exceptSessionId: current.id });
    if (calls === 1) { await gate; await route.fulfill({ status: 403, json: { detail: "Revoke denied" } }); }
    else { done = true; await route.fulfill({ json: { revokedCount: 1 } }); }
  });
  await page.goto("/settings/sessions");
  const all = page.getByRole("button", { name: "Sign out everywhere else", exact: true });
  await all.click();
  await expect(page.getByRole("button", { name: /Revoking/ })).toBeDisabled();
  release();
  await expect(page.getByText("Revoke denied", { exact: true })).toBeVisible();
  await all.click();
  await expect(all).toHaveCount(0);
  await expect(page.getByText("This device", { exact: true })).toBeVisible();
  expect(calls).toBe(2);
});
