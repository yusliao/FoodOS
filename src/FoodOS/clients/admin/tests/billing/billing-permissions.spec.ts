import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Billing.View";
const manage = "Permissions.Billing.Manage";
const invoice = { id: "inv-1", tenantId: "customer-a", invoiceNumber: "INV-1", periodYear: 2026, periodMonth: 9, currency: "USD", subtotalAmount: 20, status: "Draft", createdAtUtc: "2026-09-01T00:00:00Z", lineItems: [], purpose: "Subscription" };
const plan = { id: "plan-1", key: "basic", name: "Basic", currency: "USD", monthlyBasePrice: 20, interval: "Monthly", overageRates: {}, isActive: true };

async function setup(page: Page, permissions = [view]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: string[] = [];
  await page.route("**/api/v1/billing/**", async route => {
    expect(route.request().headers().tenant).toBe("root");
    const path = new URL(route.request().url()).pathname;
    requests.push(path);
    return route.fulfill({ json: path.endsWith("/plans") ? [plan] : invoice });
  });
  return requests;
}

for (const path of ["/billing/plans", "/billing/invoices/inv-1"]) {
  test(`Manage without View cannot access ${path}`, async ({ page }) => {
    const requests = await setup(page, [manage]);
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
    expect(requests).toEqual([]);
  });
}

test("plans failure is not empty or zero statistics and supports retry", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/billing/plans?*", route => fail
    ? route.fulfill({ status: 403, json: { detail: "Plans denied" } }) : route.fallback());
  await page.goto("/billing/plans");
  await expect(page.getByText("Plans denied", { exact: true })).toBeVisible();
  await expect(page.getByRole("main").getByText("$0.00", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("main").getByText(/No plans/)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /New plan|Edit Basic/ })).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("main").getByText("Basic", { exact: true })).toBeVisible();
});

test("invoice 404 has retry and no stale writes", async ({ page }) => {
  await setup(page, [view, manage]);
  let fail = true;
  await page.route("**/api/v1/billing/invoices/inv-1", route => fail
    ? route.fulfill({ status: 404, json: { detail: "Invoice not found" } }) : route.fallback());
  await page.goto("/billing/invoices/inv-1");
  await expect(page.getByText("Invoice not found", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: /Issue invoice|Mark as paid|Void invoice|Download PDF/i })).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
});

test("viewer PDF uses token root identity despite edited tenant cache and refreshes 401", async ({ page }) => {
  await setup(page);
  let calls = 0;
  await page.route("**/api/v1/billing/invoices/inv-1/pdf", route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().headers()["accept-language"]).toBe("en-US");
    calls++;
    return calls === 1
      ? route.fulfill({ status: 401, json: { detail: "Expired" } })
      : route.fulfill({ contentType: "application/pdf", body: "%PDF-1.4 mock" });
  });
  let refreshes = 0;
  await page.route("**/api/v1/identity/token/refresh", async route => {
    refreshes++;
    expect(route.request().headers().tenant).toBe("root");
    const token = route.request().postDataJSON().token;
    await route.fulfill({ json: { token, refreshToken: "rotated" } });
  });
  await page.goto("/billing/invoices/inv-1");
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
  await page.evaluate(() => localStorage.setItem("fsh.admin.tenant", "customer-a"));
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download PDF", exact: true }).click();
  expect((await download).suggestedFilename()).toBe("INV-1.pdf");
  expect(calls).toBe(2);
  expect(refreshes).toBe(1);
  await expect(page.getByRole("button", { name: /Issue invoice|Mark as paid|Void invoice/i })).toHaveCount(0);
});

test("PDF failure reports server problem and supports another download", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/billing/invoices/inv-1/pdf", route => fail
    ? route.fulfill({ status: 403, json: { detail: "PDF denied" } })
    : route.fulfill({ contentType: "application/pdf", body: "%PDF-1.4 mock" }));
  await page.goto("/billing/invoices/inv-1");
  await page.getByRole("button", { name: "Download PDF", exact: true }).click();
  await expect(page.getByText("PDF denied", { exact: true })).toBeVisible();
  fail = false;
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download PDF", exact: true }).click();
  expect((await download).suggestedFilename()).toBe("INV-1.pdf");
});

test("failed void preserves reason, pending write disables competing issue", async ({ page }) => {
  await setup(page, [view, manage]);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/billing/invoices/inv-1/void", async route => {
    expect(route.request().postDataJSON()).toEqual({ reason: "duplicate" });
    expect(route.request().headers().tenant).toBe("root");
    await pending;
    await route.fulfill({ status: 403, json: { detail: "Void denied" } });
  });
  await page.goto("/billing/invoices/inv-1");
  await page.getByLabel("Reason", { exact: true }).fill("duplicate");
  await page.getByRole("button", { name: "Void invoice", exact: true }).click();
  await expect(page.getByRole("button", { name: "Issue invoice", exact: true })).toBeDisabled();
  release();
  await expect(page.getByText("Void denied", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Reason", { exact: true })).toHaveValue("duplicate");
  await expect(page.getByRole("button", { name: "Issue invoice", exact: true })).toBeEnabled();
});

test("Chinese mobile plans and invoice retain primary layout", async ({ page }) => {
  await setup(page, [view, manage]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  for (const [path, content] of [["/billing/plans", "Basic"], ["/billing/invoices/inv-1", "INV-1"]]) {
    await page.goto(path);
    await expect(page.getByRole("main").getByText(content, { exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
});
