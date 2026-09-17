import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Billing.View";
const invoice = { id: "inv-1", tenantId: "customer-a", invoiceNumber: "INV-1", periodYear: 2026, periodMonth: 9, currency: "USD", subtotalAmount: 20, status: "Issued", createdAtUtc: "2026-09-01T00:00:00Z", lineItems: [], purpose: "Subscription" };
async function setup(page: Page, permissions = [view]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
}

test("invoice list denies Manage without View without requests", async ({ page }) => {
  await setup(page, ["Permissions.Billing.Manage"]);
  let calls = 0;
  await page.route("**/api/v1/billing/invoices?*", route => { calls++; return route.fulfill({ json: paged([]) }); });
  await page.goto("/billing/invoices");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(calls).toBe(0);
});

test("invoice failure is not zero or empty, retry restores list", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/billing/invoices?*", route => fail
    ? route.fulfill({ status: 500, json: { detail: "Invoices unavailable" } })
    : route.fulfill({ json: paged([invoice]) }));
  await page.goto("/billing/invoices");
  await expect(page.getByText("Invoices unavailable", { exact: true })).toBeVisible();
  await expect(page.getByText(/No invoices match/)).toHaveCount(0);
  await expect(page.getByText("$0.00", { exact: true })).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
});

test("current page sums remain separate by currency and status", async ({ page }) => {
  await setup(page);
  await page.route("**/api/v1/billing/invoices?*", route => route.fulfill({ json: paged([
    invoice,
    { ...invoice, id: "inv-2", invoiceNumber: "INV-2", currency: "EUR", subtotalAmount: 30, status: "Paid" },
    { ...invoice, id: "inv-3", invoiceNumber: "INV-3", subtotalAmount: 10, status: "Void" },
  ], { totalCount: 100 }) }));
  await page.goto("/billing/invoices");
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
  const billed = page.getByText("Billed", { exact: true }).locator("..");
  await expect(billed).toContainText(/EUR\s+30\.00/);
  await expect(billed).toContainText(/USD\s+30\.00/);
  await expect(billed).not.toContainText("60.00");
  await expect(billed).toContainText("including draft and void");
  const outstanding = page.getByText("Outstanding", { exact: true }).locator("..");
  await expect(outstanding).toContainText(/USD\s+20\.00/);
  await expect(outstanding).not.toContainText("EUR");
});

test("plan averages are calculated independently for each currency", async ({ page }) => {
  await setup(page);
  const plan = { id: "p1", key: "p1", name: "First", currency: "USD", monthlyBasePrice: 10, interval: "Monthly", overageRates: {}, isActive: true };
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [plan, { ...plan, id: "p2", key: "p2", monthlyBasePrice: 30 }, { ...plan, id: "p3", key: "p3", currency: "EUR", monthlyBasePrice: 90 }] }));
  await page.goto("/billing/plans");
  await expect(page.getByText(/USD\s+20\.00/, { exact: true })).toBeVisible();
  await expect(page.getByText(/EUR\s+90\.00/, { exact: true })).toBeVisible();
  await expect(page.getByText(/43\.33/)).toHaveCount(0);
});

test("paging and tenant/date filters use root identity and hide previous results while loading", async ({ page }) => {
  await setup(page);
  const requests: URL[] = [];
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/billing/invoices?*", async route => {
    expect(route.request().headers().tenant).toBe("root");
    const url = new URL(route.request().url());
    requests.push(url);
    const pageNumber = Number(url.searchParams.get("pageNumber"));
    const customer = url.searchParams.get("tenantId");
    if (customer) await pending;
    await route.fulfill({ json: paged([{ ...invoice, invoiceNumber: customer ? "FILTERED" : `PAGE-${pageNumber}` }], { pageNumber, totalCount: 21, pageSize: 20 }) });
  });
  await page.goto("/billing/invoices");
  await expect(page.getByText("PAGE-1", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.getByText("PAGE-2", { exact: true })).toBeVisible();
  await page.getByLabel("Tenant", { exact: true }).fill("customer-b");
  await expect(page.getByText("PAGE-2", { exact: true })).toHaveCount(0);
  release();
  await expect(page.getByText("FILTERED", { exact: true })).toBeVisible();
  expect(requests.at(-1)?.searchParams.get("pageNumber")).toBe("1");
  expect(requests.at(-1)?.searchParams.get("tenantId")).toBe("customer-b");
  await page.getByLabel("Year", { exact: true }).fill("2026");
  await page.getByLabel("Month", { exact: true }).fill("9");
  await expect.poll(() => requests.at(-1)?.searchParams.get("periodMonth")).toBe("9");
  expect(requests.at(-1)?.searchParams.get("periodYear")).toBe("2026");
  await page.getByLabel("Status", { exact: true }).click();
  await page.getByRole("menuitem", { name: "Paid", exact: true }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get("status")).toBe("Paid");
});

test("Chinese mobile invoice list shows per-currency totals without overflow", async ({ page }) => {
  await setup(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  const longNumber = "INV-" + "2026".repeat(12);
  await page.route("**/api/v1/billing/invoices?*", route => route.fulfill({ json: paged([
    { ...invoice, invoiceNumber: longNumber },
    { ...invoice, id: "inv-2", invoiceNumber: "INV-2", currency: "EUR", subtotalAmount: 30 },
  ]) }));
  await page.goto("/billing/invoices");
  await expect(page.getByText(longNumber, { exact: true })).toBeVisible();
  await expect(page.getByText("当前页票面金额（含草稿和已作废），按币种分别汇总，不代表收入。", { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("invalid date filters block requests and clear returns the list", async ({ page }) => {
  await setup(page);
  let calls = 0;
  await page.route("**/api/v1/billing/invoices?*", route => { calls++; return route.fulfill({ json: paged([invoice]) }); });
  await page.goto("/billing/invoices");
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
  const initial = calls;
  await page.getByLabel("Month", { exact: true }).fill("0");
  await expect(page.getByRole("alert")).toContainText("month from 1 to 12");
  await expect(page.getByText("INV-1", { exact: true })).toHaveCount(0);
  expect(calls).toBe(initial);
  await page.getByRole("button", { name: "Clear", exact: true }).click();
  await expect(page.getByText("INV-1", { exact: true })).toBeVisible();
});
