import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const support = "Permissions.Users.Impersonate";
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01" } }));
  await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ status: 404, json: {} }));
}

test("support-only operator uses explicit target and root identity without ordinary user search", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", support]);
  const ordinary: string[] = [];
  page.on("request", r => { if (r.url().includes("/users/search")) ordinary.push(r.url()); });
  await page.route("**/api/v1/identity/impersonation/users?*", route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(new URL(route.request().url()).searchParams.get("targetTenantId")).toBe("acme");
    return route.fulfill({ json: paged([{ id: "alice", userName: "alice", email: "alice@acme.example", isActive: true }]) });
  });
  await page.goto("/tenants/acme");
  await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
  await expect(page.getByText("alice@acme.example", { exact: true })).toBeVisible();
  expect(ordinary).toEqual([]);
});

test("lookup failure retries, search empty is not a failure", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", support]);
  let failed = true;
  await page.route("**/api/v1/identity/impersonation/users?*", route => route.fulfill(failed
    ? { status: 403, json: { detail: "Target unavailable" } } : { json: paged([]) }));
  await page.goto("/tenants/acme");
  await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
  await expect(page.getByText("Target unavailable", { exact: true })).toBeVisible();
  failed = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("Target unavailable", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("dialog").getByRole("button", { name: "Cancel", exact: true })).toBeVisible();
});

test("ordinary user-search permission does not permit support lookup", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", "Permissions.Users.Search"]);
  let calls = 0;
  page.on("request", r => { if (r.url().includes("/impersonation/users")) calls++; });
  await page.goto("/tenants/acme");
  await expect(page.getByRole("heading", { name: "Acme", level: 1 })).toBeVisible();
  await expect(page.getByRole("button", { name: "Impersonate user", exact: true })).toHaveCount(0);
  expect(calls).toBe(0);
});
