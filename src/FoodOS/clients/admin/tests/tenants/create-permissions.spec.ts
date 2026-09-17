import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Tenants.View";
const create = "Permissions.Tenants.Create";
const billing = "Permissions.Billing.View";
const plan = { id: "free", key: "free", name: "Free", currency: "USD", monthlyBasePrice: 0, isActive: true, interval: "Monthly" };

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/tenants/?*", route => route.fulfill({ json: paged([]) }));
}
async function openAndFill(page: Page) {
  await page.goto("/tenants");
  await page.getByRole("button", { name: "New tenant", exact: true }).click();
  await page.getByLabel(/^Display name/).fill("Acme Corp");
  await page.getByLabel(/^Admin email/).fill("admin@acme.example");
  await page.getByLabel(/^Initial admin password/).fill("Password123!");
}

test("View plus Billing.Manage cannot create or query plans", async ({ page }) => {
  await setup(page, [view, billing, "Permissions.Billing.Manage"]);
  const requests: string[] = [];
  page.on("request", r => { if (r.url().includes("/billing/plans") || r.method() === "POST") requests.push(r.url()); });
  await page.goto("/tenants");
  await expect(page.getByRole("heading", { name: "Registry", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "New tenant", exact: true })).toHaveCount(0);
  expect(requests).toEqual([]);
});

test("Create without Billing.View submits default plan with root identity", async ({ page }) => {
  await setup(page, [view, create]);
  let planRequests = 0;
  page.on("request", r => { if (r.url().includes("/billing/plans")) planRequests++; });
  await page.route("**/api/v1/tenants/", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toMatchObject({ id: "acme-corp", planKey: null });
    await route.fulfill({ status: 403, json: { detail: "Create denied" } });
  });
  await openAndFill(page);
  await expect(page.getByText(/configured default plan/)).toBeVisible();
  await page.getByRole("button", { name: "Create tenant", exact: true }).click();
  await expect(page.getByText("Create denied", { exact: true })).toBeVisible();
  await expect(page.getByLabel(/^Display name/)).toHaveValue("Acme Corp");
  expect(planRequests).toBe(0);
});

test("plan failure blocks creation until retry and valid selection", async ({ page }) => {
  await setup(page, [view, create, billing]);
  let failed = true;
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill(failed
    ? { status: 403, json: { detail: "Plan catalog denied" } }
    : { json: [plan] }));
  await openAndFill(page);
  await expect(page.getByText("Plan catalog denied")).toBeVisible();
  await expect(page.getByRole("button", { name: "Create tenant", exact: true })).toBeDisabled();
  failed = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: "Create tenant", exact: true })).toBeEnabled();
});

for (const plans of [[], [{ ...plan, isActive: false }], [{ ...plan, key: "x".repeat(65) }]]) {
  test(`empty, inactive or incompatible plan blocks creation: ${JSON.stringify(plans)}`, async ({ page }) => {
    await setup(page, [view, create, billing]);
    await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: plans }));
    await openAndFill(page);
    await expect(page.getByRole("button", { name: "Create tenant", exact: true })).toBeDisabled();
    if ((plans[0]?.key.length ?? 0) > 64) await expect(page.getByText(/not accepted by the tenant creation API/)).toBeVisible();
  });
}

for (const key of ["x", "legacy_plan", " spaced "]) {
  test(`creation preserves supported catalog key: ${key}`, async ({ page }) => {
    await setup(page, [view, create, billing]);
    await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [{ ...plan, key }] }));
    let submitted: unknown;
    await page.route("**/api/v1/tenants/", async route => {
      submitted = route.request().postDataJSON();
      await route.fulfill({ status: 403, json: { detail: "Recorded payload" } });
    });
    await openAndFill(page);
    await page.getByRole("button", { name: "Create tenant", exact: true }).click();
    await expect(page.getByText("Recorded payload", { exact: true })).toBeVisible();
    expect(submitted).toMatchObject({ planKey: key });
  });
}

test("pending creation freezes mobile form and prevents dismissal, failure permits retry", async ({ page }) => {
  await setup(page, [view, create]);
  await page.setViewportSize({ width: 390, height: 844 });
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let writes = 0;
  await page.route("**/api/v1/tenants/", async route => {
    writes++;
    await gate;
    await route.fulfill({ status: 500, json: { detail: "Provisioning failed" } });
  });
  await openAndFill(page);
  await page.getByRole("button", { name: "Create tenant", exact: true }).click();
  await expect(page.getByLabel(/^Display name/)).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toBeVisible();
  expect(writes).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  release();
  await expect(page.getByText("Provisioning failed", { exact: true })).toBeVisible();
  await expect(page.getByLabel(/^Display name/)).toHaveValue("Acme Corp");
  await expect(page.getByRole("button", { name: "Create tenant", exact: true })).toBeEnabled();
});
