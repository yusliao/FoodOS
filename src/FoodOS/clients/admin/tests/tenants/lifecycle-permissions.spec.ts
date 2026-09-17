import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Tenants.View";
const update = "Permissions.Tenants.Update";
const tenant = { id: "acme", name: "Acme", adminEmail: "admin@acme.example", isActive: true, validUpto: "2027-01-01" };
const failed = { tenantId: "acme", status: "Failed", currentStep: "Seed", steps: [], error: "Seed failed" };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/tenants/?*", route => route.fulfill({ json: paged([tenant]) }));
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: tenant }));
  await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ json: failed }));
}

test("no View blocks direct detail and list without tenant requests", async ({ page }) => {
  await setup(page, [update]);
  const requests: string[] = [];
  page.on("request", r => { if (r.url().includes("/api/v1/tenants")) requests.push(r.url()); });
  for (const url of ["/tenants", "/tenants/acme"]) {
    await page.goto(url);
    await expect(page.getByRole("button", { name: "Deactivate tenant", exact: true })).toHaveCount(0);
    await expect(page.getByText("Permissions.Tenants.View", { exact: true })).toBeVisible();
  }
  expect(requests).toEqual([]);
});

test("View-only failed provisioning has no write actions", async ({ page }) => {
  await setup(page, [view]);
  await page.goto("/tenants/acme");
  await expect(page.getByText("Seed failed", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: /Deactivate|Retry provisioning/ })).toHaveCount(0);
});

for (const status of [403, 404, 500]) {
  test(`detail ${status} can retry without loading dependent provisioning`, async ({ page }) => {
    await setup(page, [view, update]);
    let broken = true;
    let provisioningCalls = 0;
    await page.route("**/api/v1/tenants/acme/status", route => route.fulfill(broken
      ? { status, json: { detail: "Status unavailable" } } : { json: tenant }));
    page.on("request", r => { if (r.url().endsWith("/provisioning")) provisioningCalls++; });
    await page.goto("/tenants/acme");
    await expect(page.getByText("Status unavailable", { exact: true })).toBeVisible({ timeout: 15000 });
    expect(provisioningCalls).toBe(0);
    await expect(page.getByRole("button", { name: "Deactivate tenant", exact: true })).toHaveCount(0);
    broken = false;
    await page.getByRole("button", { name: "Retry", exact: true }).click();
    await expect(page.getByRole("button", { name: "Deactivate tenant", exact: true })).toBeVisible();
  });
}

test("list failure retries without showing an empty result", async ({ page }) => {
  await setup(page, [view]);
  let broken = true;
  await page.route("**/api/v1/tenants/?*", route => route.fulfill(broken
    ? { status: 403, json: { detail: "Registry denied" } } : { json: paged([tenant]) }));
  await page.goto("/tenants");
  await expect(page.getByText("Registry denied", { exact: true })).toBeVisible();
  await expect(page.getByText("No tenants yet.", { exact: true })).toHaveCount(0);
  broken = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: /Acme/ })).toBeVisible();
});

test("activation preserves confirmation on failure then refreshes state on success", async ({ page }) => {
  await setup(page, [view, update]);
  let active = true;
  let denied = true;
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { ...tenant, isActive: active } }));
  await page.route("**/api/v1/tenants/acme/activation", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ tenantId: "acme", isActive: false });
    if (denied) await route.fulfill({ status: 403, json: { detail: "Activation denied" } });
    else { active = false; await route.fulfill({ json: { tenantId: "acme", isActive: false } }); }
  });
  await page.goto("/tenants/acme");
  await page.getByRole("button", { name: "Deactivate tenant", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Deactivate tenant", exact: true }).click();
  await expect(page.getByText("Activation denied", { exact: true })).toBeVisible();
  await expect(page.getByRole("dialog")).toBeVisible();
  denied = false;
  await page.getByRole("dialog").getByRole("button", { name: "Deactivate tenant", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Activate tenant", exact: true })).toBeVisible();
});

test("provisioning query 403 stops polling, Retry recovers and write uses root", async ({ page }) => {
  await setup(page, [view, update]);
  let broken = true;
  let done = false;
  let calls = 0;
  await page.route("**/api/v1/tenants/acme/provisioning", route => {
    calls++;
    return route.fulfill(broken ? { status: 403, json: { detail: "Pipeline denied" } }
      : { json: done ? { ...failed, status: "Completed", error: null } : failed });
  });
  await page.route("**/api/v1/tenants/acme/provisioning/retry", async route => {
    expect(route.request().headers().tenant).toBe("root");
    done = true;
    await route.fulfill({ json: { ...failed, status: "Completed", error: null } });
  });
  await page.goto("/tenants/acme");
  await expect(page.getByText("Pipeline denied", { exact: true })).toBeVisible();
  await page.waitForTimeout(2200); // Deliberately observe one former polling interval.
  expect(calls).toBe(1);
  broken = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await page.getByRole("button", { name: "Retry provisioning", exact: true }).click();
  await expect(page.getByRole("button", { name: "Retry provisioning", exact: true })).toHaveCount(0);
});

test("root activation is disabled", async ({ page }) => {
  await setup(page, [view, update]);
  await page.route("**/api/v1/tenants/root/status", route => route.fulfill({ json: { ...tenant, id: "root" } }));
  await page.route("**/api/v1/tenants/root/provisioning", route => route.fulfill({ status: 404, json: {} }));
  await page.goto("/tenants/root");
  await expect(page.getByRole("button", { name: "Deactivate tenant", exact: true })).toBeDisabled();
});
