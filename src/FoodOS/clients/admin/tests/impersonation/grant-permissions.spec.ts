import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Impersonation.View";
const revoke = "Permissions.Impersonation.Revoke";
const grant = {
  id: "grant-one", jti: "one", actorUserId: "actor", actorTenantId: "root",
  impersonatedUserId: "target", impersonatedUserName: "alice@acme.example",
  impersonatedTenantId: "acme", reason: "Support", startedAtUtc: "2026-09-17T00:00:00Z",
  expiresAtUtc: "2027-01-01T00:00:00Z", status: "Active",
};
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
}

test("no View blocks direct URL without requests; read-only cannot revoke", async ({ page }) => {
  await setup(page, [revoke]);
  let calls = 0;
  await page.route("**/api/v1/identity/impersonation/grants?*", route => {
    calls++;
    return route.fulfill({ json: [grant] });
  });
  await page.goto("/impersonation");
  await expect(page.getByText(view, { exact: true })).toBeVisible();
  expect(calls).toBe(0);
  await setup(page, [view]);
  await page.goto("/impersonation");
  await expect(page.getByText(grant.impersonatedUserName, { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: /Revoke/ })).toHaveCount(0);
});

test("failed refresh hides stale grants and counts then recovers", async ({ page }) => {
  await setup(page, [view, revoke]);
  let failed = false;
  await page.route("**/api/v1/identity/impersonation/grants?*", route => route.fulfill(failed
    ? { status: 403, json: { detail: "Grant access denied" } } : { json: [grant] }));
  await page.goto("/impersonation");
  await expect(page.getByText(grant.impersonatedUserName, { exact: true })).toBeVisible();
  failed = true;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText("Grant access denied", { exact: true })).toBeVisible();
  await expect(page.getByText(grant.impersonatedUserName, { exact: true })).toHaveCount(0);
  await expect(page.getByText("in-flight tokens", { exact: true })).toHaveCount(0);
  failed = false;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText(grant.impersonatedUserName, { exact: true })).toBeVisible();
});

test("revoke failure preserves reason and root identity then succeeds", async ({ page }) => {
  await setup(page, [view, revoke]);
  let done = false;
  let denied = true;
  await page.route("**/api/v1/identity/impersonation/grants?*", route => route.fulfill({ json: done ? [] : [grant] }));
  await page.route("**/api/v1/identity/impersonation/grants/grant-one/revoke", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ reason: "Resolved" });
    if (denied) await route.fulfill({ status: 403, json: { detail: "Revocation denied" } });
    else { done = true; await route.fulfill({ json: { ...grant, status: "Revoked" } }); }
  });
  await page.goto("/impersonation");
  await page.getByRole("button", { name: /Revoke/ }).click();
  await page.locator("#revoke-reason").fill("Resolved");
  await page.getByRole("button", { name: "Revoke now", exact: true }).click();
  await expect(page.getByText("Revocation denied", { exact: true })).toBeVisible();
  await expect(page.locator("#revoke-reason")).toHaveValue("Resolved");
  denied = false;
  await page.getByRole("button", { name: "Revoke now", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
});

test("tenant active-grants error is visible and retryable with explicit resource filter", async ({ page }) => {
  await setup(page, [view, "Permissions.Tenants.View"]);
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01" } }));
  await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ status: 404, json: {} }));
  let failed = true;
  await page.route("**/api/v1/identity/impersonation/grants?*", route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(new URL(route.request().url()).searchParams.get("ImpersonatedTenantId")).toBe("acme");
    return route.fulfill(failed ? { status: 403, json: { detail: "Active grants denied" } } : { json: [grant] });
  });
  await page.goto("/tenants/acme");
  await expect(page.getByText("Active grants denied", { exact: true })).toBeVisible();
  failed = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText(grant.impersonatedUserName, { exact: true })).toBeVisible();
});
