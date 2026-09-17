import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Tenants.View";
const renew = "Permissions.Tenants.UpgradeSubscription";
const billingView = "Permissions.Billing.View";
const plan = { id: "pro", key: "pro", name: "Pro", currency: "USD", monthlyBasePrice: 20, overageRates: {}, isActive: true, interval: "Monthly" };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/tenants/theme?*", route => route.fulfill({ status: 403, json: { detail: "Theme not in this test scope" } }));
  const writes: { path: string; body: unknown }[] = [];
  let planCalls = 0;
  await page.route("**/api/v1/billing/plans?*", route => {
    planCalls++;
    expect(route.request().headers().tenant).toBe("root");
    return route.fulfill({ json: [plan] });
  });
  await page.route("**/api/v1/tenants/acme/**", route => {
    expect(route.request().headers().tenant).toBe("root");
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/status")) return route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01T00:00:00Z", plan: "pro", expiryState: "Active" } });
    if (path.endsWith("/provisioning")) return route.fulfill({ status: 404, json: {} });
    writes.push({ path, body: route.request().postDataJSON() });
    return route.fulfill({ json: { tenantId: "acme", validUpto: "2027-02-01T00:00:00Z", planKey: "pro", planChanged: false } });
  });
  await page.goto("/tenants/acme");
  await expect(page.getByRole("heading", { name: "Acme", exact: true, level: 1 })).toBeVisible();
  return { writes, planCalls: () => planCalls };
}

test("renewal passes a single-character catalog key unchanged", async ({ page }) => {
  const state = await setup(page, [view, renew, billingView]);
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [{ ...plan, key: "x" }] }));
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await page.locator("#renew-plan").click();
  await page.getByRole("menuitem", { name: /Pro/ }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Change plan & renew", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(state.writes).toEqual([{ path: "/api/v1/tenants/acme/renew", body: { tenantId: "acme", planKey: "x" } }]);
});

test("Billing.Manage cannot grant tenant subscription actions", async ({ page }) => {
  const state = await setup(page, [view, billingView, "Permissions.Billing.Manage"]);
  await expect(page.getByRole("button", { name: /Renew \/ change plan|Adjust validity/ })).toHaveCount(0);
  expect(state.planCalls()).toBe(0);
  expect(state.writes).toEqual([]);
});

test("UpgradeSubscription without Billing.View renews current plan without querying prices", async ({ page }) => {
  const state = await setup(page, [view, renew]);
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await expect(page.getByText(/No plan-view permission/)).toBeVisible();
  await expect(page.locator("#renew-plan")).toHaveCount(0);
  await page.getByRole("dialog").getByRole("button", { name: "Renew", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(state.planCalls()).toBe(0);
  expect(state.writes).toEqual([{ path: "/api/v1/tenants/acme/renew", body: { tenantId: "acme", planKey: null } }]);
});

test("plan failure blocks default selection, retry enables valid plan", async ({ page }) => {
  const state = await setup(page, [view, renew, billingView]);
  let fail = true;
  await page.route("**/api/v1/billing/plans?*", route => fail
    ? route.fulfill({ status: 403, json: { detail: "Plans denied" } }) : route.fallback());
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await expect(page.getByText("Plans denied", { exact: true })).toBeVisible();
  await expect(page.getByRole("dialog").getByRole("button", { name: "Renew", exact: true })).toBeDisabled();
  expect(state.writes).toEqual([]);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("button", { name: "Renew", exact: true })).toBeEnabled();
});

test("empty or inactive plan list cannot submit current plan", async ({ page }) => {
  const state = await setup(page, [view, renew, billingView]);
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [{ ...plan, isActive: false }] }));
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("button", { name: "Renew", exact: true })).toBeDisabled();
  await expect(page.locator("#renew-plan")).toBeDisabled();
  expect(state.writes).toEqual([]);
});

test("renew failure preserves selection; closing and reopening resets to current plan", async ({ page }) => {
  await setup(page, [view, renew, billingView]);
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [plan, { ...plan, id: "team", key: "team", name: "Team" }] }));
  await page.route("**/api/v1/tenants/acme/renew", route => {
    expect(route.request().postDataJSON()).toEqual({ tenantId: "acme", planKey: "team" });
    return route.fulfill({ status: 500, json: { detail: "Renew failed" } });
  });
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await page.locator("#renew-plan").click();
  await page.getByRole("menuitem", { name: /Team/ }).click();
  await page.getByRole("dialog").getByRole("button", { name: /Change.*renew/i }).click();
  await expect(page.getByText("Renew failed", { exact: true }).first()).toBeVisible();
  await expect(page.locator("#renew-plan")).toContainText("Team");
  await page.getByRole("dialog").getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Renew / change plan", exact: true }).click();
  await expect(page.locator("#renew-plan")).toContainText("Pro");
});

test("validity adjustment needs no Billing permission and preserves failed date", async ({ page }) => {
  const state = await setup(page, [view, renew]);
  let fail = true;
  await page.route("**/api/v1/tenants/acme/adjust-validity", route => fail
    ? route.fulfill({ status: 403, json: { detail: "Adjustment denied" } }) : route.fallback());
  await page.getByRole("button", { name: "Adjust validity", exact: true }).click();
  await page.locator("#av-validUpto").fill("2026-01-01");
  await page.getByRole("dialog").getByRole("button", { name: "Adjust validity", exact: true }).click();
  await expect(page.getByText("Adjustment denied", { exact: true })).toBeVisible();
  await expect(page.locator("#av-validUpto")).toHaveValue("2026-01-01");
  fail = false;
  await page.getByRole("dialog").getByRole("button", { name: "Adjust validity", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(state.planCalls()).toBe(0);
  expect(state.writes[0].body).toEqual({ tenantId: "acme", validUpto: "2026-01-01T00:00:00.000Z" });
});
