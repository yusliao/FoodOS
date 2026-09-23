import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installShellMocks } from "../helpers/shell-mocks";
const status = { mode: "externalWms", readiness: "notConfigured", acceptsOrders: false, acceptsOrderChanges: false, localWarehouseExecution: false };
for (const granted of [true, false]) test(`legacy execution is absent from navigation and commands (grants=${granted})`, async ({ page }) => {
  const permissions = granted ? ["Permissions.Procurement.Purchase.View", "Permissions.Warehouse.Putaway.View", "Permissions.Warehouse.Waves.View", "Permissions.Warehouse.Picks.View", "Permissions.Logistics.Shipments.View"] : [];
  await page.route("**/api/v1/identity/permissions", route => route.fulfill({ json: permissions }));
  await page.goto("/ops/qc");
  await expect(page.getByRole("heading", { name: "Warehouse integration" })).toBeVisible();
  if (granted) {
    await page.getByRole("button", { name: "Fulfillment", exact: true }).click();
    await expect(page.getByRole("link", { name: "Purchasing", exact: true })).toBeVisible();
  }
  for (const path of ["qc", "putaway", "waves", "picks", "shipments"]) {
    await expect(page.locator(`a[href="/ops/${path}"]`)).toHaveCount(0);
  }
  await page.keyboard.press("Control+k");
  await expect(page.getByRole("combobox", { name: "Search commands" })).toBeVisible();
  if (granted) await expect(page.getByRole("option", { name: /Purchasing/ })).toBeVisible();
  for (const name of [/Quality desk/, /Putaway/, /Waves/, /Pick tasks/, /Load & POD/]) {
    await expect(page.getByRole("option", { name })).toHaveCount(0);
  }
});
test("existing order remains readable without amendment or cancellation", async ({ page }) => {
  await page.route("**/api/v1/shop/stores**", route => route.fulfill({ json: [{ id: "store-1", name: "Kitchen", code: "S1", address: "1 Main St" }] }));
  await page.route("**/api/v1/shop/orders/order-1", route => route.fulfill({ json: { id: "order-1", number: "SO-WMS", storeId: "store-1", status: "Reserved", cutoffAt: "2099-01-01T00:00:00Z", businessDate: "2026-09-17", revision: 1, lines: [] } }));
  const writes: string[] = [];
  page.on("request", request => { if (request.method() !== "GET" && request.url().includes("/shop/orders")) writes.push(request.url()); });
  await page.goto("/shop/orders/order-1");
  await expect(page.getByRole("heading", { name: "SO-WMS" })).toBeVisible();
  await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
  await expect(page.getByRole("button", { name: /Save changes|Cancel order/ })).toHaveCount(0);
  expect(writes).toEqual([]);
});
test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", route => route.fulfill({
    json: ["Permissions.Ordering.Shop.View", "Permissions.Ordering.Shop.Order"],
  }));
  await page.route("**/api/v1/fulfillment/capabilities", route => route.fulfill({ json: status }));
});

for (const path of ["qc", "putaway", "waves", "picks", "shipments"]) test("direct legacy route does not mount execution page: " + path, async ({ page }) => {
  const requests: string[] = [];
  page.on("request", request => { if (/api\/v1\/(procurement|warehouse|inventory|logistics)/.test(request.url())) requests.push(request.url()); });
  await page.goto("/ops/" + path);
  await expect(page.getByRole("heading", { name: "Warehouse integration" })).toBeVisible();
  await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
  expect(requests).toEqual([]);
});

test("Chinese mobile WMS error retries without enabling local work", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  let fail = true;
  await page.route("**/api/v1/fulfillment/capabilities", route => route.fulfill(fail ? { status: 500, json: {} } : { json: status }));
  await page.goto("/ops/qc");
  await expect(page.getByText("无法确认 WMS 状态，执行操作继续禁用。")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "重试 WMS 状态" }).click();
  await expect(page.getByText("WMS 对接尚未就绪，暂不能确认库存及配送承诺。")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});

test("cart can be viewed but cannot place an order when WMS status is unknown", async ({ page }) => {
  await page.route("**/api/v1/shop/stores**", route => route.fulfill({ json: [{ id: "store-1", name: "Kitchen", code: "S1", address: "1 Main St" }] }));
  await page.route("**/api/v1/shop/stores/store-1/cart", route => route.fulfill({ json: { storeId: "store-1", lines: [{ productId: "product-1", quantity: 2 }] } }));
  await page.route("**/api/v1/shop/products/product-1**", route => route.fulfill({ json: { id: "product-1", name: "Apple", sku: "P1", unitPrice: 2, currency: "USD", priceSource: "Catalog", baseUom: "ea", isAvailable: true } }));
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/fulfillment/capabilities", async route => { await pending; await route.fulfill({ status: 403, json: {} }); });
  const writes: string[] = [];
  page.on("request", request => { if (request.method() === "POST" && request.url().includes("/shop/orders")) writes.push(request.url()); });
  await page.goto("/shop/cart");
  await expect(page.getByText("Checking WMS readiness…")).toBeVisible();
  await expect(page.getByRole("button", { name: "Place order", exact: true })).toBeDisabled();
  release();
  await expect(page.getByText("WMS status could not be verified. Execution remains disabled.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Place order", exact: true })).toBeDisabled();
  expect(writes).toEqual([]);
});
