import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

const PLV = "Permissions.Catalog.PriceLists.View";
const PLC = "Permissions.Catalog.PriceLists.Create";
const PLU = "Permissions.Catalog.PriceLists.Update";
const PV = "Permissions.Catalog.Products.View";
const CV = "Permissions.Ordering.Customers.View";
const customer = { id: "11111111-1111-1111-1111-111111111111", customerTenantId: "acme", code: "ACME", name: "Acme Restaurant", creditHold: false, createdAtUtc: "2026-09-16T00:00:00Z" };
const product = { id: "22222222-2222-2222-2222-222222222222", sku: "VEG-01", name: "Baby spinach", slug: "baby-spinach", description: null, brandId: "b-1", categoryId: "c-1", price: { amount: 3.5, currency: "USD" }, isActive: true, createdAtUtc: "2026-09-16T00:00:00Z" };
const priceList = { id: "33333333-3333-3333-3333-333333333333", name: "Acme contract", customerOrgId: customer.id, priority: 10, validFrom: "2026-09-01T00:00:00Z", validTo: "2026-12-31T23:59:59Z", lines: [{ id: "line-1", productId: product.id, minQty: 5, unitPrice: 2.75, currency: "USD" }] };

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await mockJsonResponse(page, "**/api/v1/catalog/price-lists**", [priceList], { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/catalog/products**", paged([product]), { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/ordering/customer-orgs**", [customer], { method: "GET" });
}

test("price-list reader does not request customer or product lookups without their permissions", async ({ page }) => {
  await setup(page, [PLV]);
  const requests: string[] = [];
  page.on("request", request => requests.push(request.url()));
  await page.goto("/catalog/pricing");
  await expect(page.getByRole("heading", { name: "Acme contract" })).toBeVisible();
  await expect(page.getByText(customer.id)).toBeVisible();
  await expect(page.getByRole("cell", { name: product.id })).toBeVisible();
  await expect(page.getByRole("button", { name: /New price list|Set price lock|Add or replace tier/ })).toHaveCount(0);
  expect(requests.some(url => url.includes("/ordering/customer-orgs") || url.includes("/catalog/products"))).toBe(false);
});

test("direct pricing URL is denied before the price-list request", async ({ page }) => {
  await setup(page, []);
  const requests: string[] = [];
  page.on("request", request => requests.push(request.url()));
  await page.goto("/catalog/pricing");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  await expect(page.getByRole("main")).toContainText(PLV);
  expect(requests.some(url => url.includes("/api/v1/catalog/price-lists"))).toBe(false);
});

test("price-list 403 is not rendered as empty and can be retried", async ({ page }) => {
  await setup(page, [PLV]);
  let fails = true;
  await page.route("**/api/v1/catalog/price-lists**", async route => route.fulfill(fails ? { status: 403, json: { detail: "Pricing access revoked" } } : { json: [] }));
  await page.goto("/catalog/pricing");
  await expect(page.getByText("Pricing access revoked")).toBeVisible();
  await expect(page.getByText("No price lists match this filter.")).toHaveCount(0);
  fails = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No price lists match this filter.")).toBeVisible();
});

test("creator without customer-view permission creates only a catalog-wide list", async ({ page }) => {
  await setup(page, [PLV, PLC]);
  let write: { body: Record<string, unknown>; tenant: string; key: string } | undefined;
  await page.route("**/api/v1/catalog/price-lists", async route => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    write = { body: route.request().postDataJSON(), tenant: route.request().headers().tenant, key: route.request().headers()["idempotency-key"] };
    await route.fulfill({ json: priceList.id });
  });
  await page.goto("/catalog/pricing");
  await page.getByRole("button", { name: "New price list" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Partner customer")).toHaveCount(0);
  await dialog.getByLabel("Price list name").fill("Autumn catalog");
  await dialog.getByRole("button", { name: "Create price list" }).click();
  await expect(dialog).toHaveCount(0);
  expect(write?.tenant).toBe("root");
  expect(write?.key).toBeTruthy();
  expect(write?.body).toMatchObject({ name: "Autumn catalog", customerOrgId: null, priority: 0, validTo: null, lines: [] });
});

test("tier upsert uses product choice, USD and the price-list update permission", async ({ page }) => {
  await setup(page, [PLV, PLU, PV]);
  let write: { body: Record<string, unknown>; tenant: string; key: string } | undefined;
  await page.route(`**/api/v1/catalog/price-lists/${priceList.id}/lines`, async route => {
    write = { body: route.request().postDataJSON(), tenant: route.request().headers().tenant, key: route.request().headers()["idempotency-key"] };
    await route.fulfill({ json: "line-2" });
  });
  await page.goto("/catalog/pricing");
  await expect(page.getByRole("cell", { name: "Baby spinach" })).toBeVisible();
  await page.getByRole("button", { name: "Add or replace tier" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Product").selectOption(product.id);
  await dialog.getByLabel("Minimum quantity").fill("10");
  await dialog.getByLabel("Unit price (USD)").fill("2.25");
  await dialog.getByRole("button", { name: "Save tier" }).click();
  await expect(dialog).toHaveCount(0);
  expect(write).toMatchObject({ tenant: "root", body: { priceListId: priceList.id, productId: product.id, minQty: 10, unitPrice: 2.25, currency: "USD" } });
  expect(write?.key).toBeTruthy();
});

test("customer price lock is an explicit write-only upsert", async ({ page }) => {
  await setup(page, [PLV, PLU, PV, CV]);
  let write: { body: Record<string, unknown>; tenant: string; key: string } | undefined;
  await page.route("**/api/v1/catalog/price-locks", async route => {
    write = { body: route.request().postDataJSON(), tenant: route.request().headers().tenant, key: route.request().headers()["idempotency-key"] };
    await route.fulfill({ json: "lock-1" });
  });
  await page.goto("/catalog/pricing");
  await expect(page.getByRole("button", { name: "Set price lock" })).toBeVisible();
  await page.getByRole("button", { name: "Set price lock" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Partner customer").selectOption(customer.id);
  await dialog.getByLabel("Product").selectOption(product.id);
  await dialog.getByLabel("Unit price (USD)").fill("1.99");
  await dialog.getByLabel("Locked until").fill("2026-10-01T12:00");
  await dialog.getByRole("button", { name: "Save price lock" }).click();
  await expect(dialog).toHaveCount(0);
  expect(write).toMatchObject({ tenant: "root", body: { customerOrgId: customer.id, productId: product.id, unitPrice: 1.99, currency: "USD" } });
  expect(write?.key).toBeTruthy();
  expect(String(write?.body.until)).toContain("2026-10-01T");
  await expect(page.getByText(/price locks cannot be listed or deleted/i)).toBeVisible();
});

test("resolved-price check shows the server source and keeps root identity", async ({ page }) => {
  await setup(page, [PLV, PV, CV]);
  await page.route("**/api/v1/catalog/quotes**", async route => {
    expect(route.request().headers().tenant).toBe("root");
    await route.fulfill({ json: { customerOrgId: customer.id, productId: product.id, quantity: 8, unitPrice: 1.99, currency: "USD", source: "Locked" } });
  });
  await page.goto("/catalog/pricing");
  const section = page.getByRole("heading", { name: "Resolved-price check" }).locator("..").locator("..");
  await section.getByLabel("Partner customer").selectOption(customer.id);
  await section.getByLabel("Product").selectOption(product.id);
  await section.getByLabel("Quantity").fill("8");
  await section.getByRole("button", { name: "Resolve quote" }).click();
  await expect(section.getByRole("status")).toContainText("$1.99");
  await expect(section.getByRole("status")).toContainText("Locked");
});

test("Chinese pricing cards fit the mobile viewport", async ({ page }, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setup(page, [PLV]);
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/catalog/pricing");
  await expect(page.getByRole("heading", { name: "协议定价" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Acme contract" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath("pricing-mobile.png"), fullPage: true });
});
