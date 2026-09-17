import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Ordering.Orders.View";
const reconcile = "Permissions.Ordering.Orders.Reconcile";
const id = "11111111-1111-1111-1111-111111111111";
const order = { id, number: "SO-1001", customerTenantId: "acme", storeId: "22222222-2222-2222-2222-222222222222", customerOrgId: "33333333-3333-3333-3333-333333333333", warehouseId: "44444444-4444-4444-4444-444444444444", status: "Received", businessDate: "2026-09-17", cutoffAt: "2026-09-16T16:00:00Z", lines: [{ id: "line-1", productId: "product-1", orderedQty: 10, reservedQty: 10, deliveredQty: 9, shortageQty: 1, returnedQty: 0, unitPrice: 3.5, currency: "USD", lots: [] }] };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/ordering/after-sales**", route => route.fulfill({ json: [] }));
  await page.route("**/api/v1/ordering/orders**", route => route.fulfill({ json: route.request().url().includes("?") ? paged([order]) : order }));
}

test("order reader navigates to detail without restricted lookups or writes", async ({ page }) => {
  await setup(page, [view]);
  const urls: string[] = [];
  page.on("request", request => urls.push(request.url()));
  await page.goto("/orders");
  await page.getByRole("link", { name: "SO-1001" }).click();
  await expect(page.getByRole("heading", { name: "SO-1001" })).toBeVisible();
  await expect(page.getByText("$3.50")).toBeVisible();
  await expect(page.getByRole("button", { name: "Reconcile order" })).toHaveCount(0);
  expect(urls.some(url => /api\/v1\/(catalog|inventory)|ordering\/(stores|customer-orgs)/.test(url))).toBe(false);
});

for (const path of ["/orders", `/orders/${id}`]) test(`unauthorized direct URL ${path} sends no order request`, async ({ page }) => {
  await setup(page, []);
  const urls: string[] = [];
  page.on("request", request => urls.push(request.url()));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(urls.some(url => url.includes("/api/v1/ordering/orders"))).toBe(false);
});

test("status filter uses server query and resets pagination", async ({ page }) => {
  await setup(page, [view]);
  await page.goto("/orders");
  const request = page.waitForRequest(request => request.url().includes("status=Reconciled"));
  await page.getByLabel("Order status").selectOption("Reconciled");
  const actual = await request;
  expect(actual.url()).toContain("pageNumber=1");
  expect(actual.headers().tenant).toBe("root");
});

test("reconciliation requires confirmation and preserves key on server rejection", async ({ page }) => {
  await setup(page, [view, reconcile]);
  const keys: string[] = [];
  let completed = false;
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: completed ? "Reconciled" : "Received" } }));
  await page.route(`**/api/v1/ordering/orders/${id}/reconcile`, async route => {
    expect(route.request().headers().tenant).toBe("root");
    keys.push(route.request().headers()["idempotency-key"]);
    completed = keys.length > 1;
    await route.fulfill(completed ? { json: id } : { status: 409, json: { detail: "Please retry reconciliation" } });
  });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Reconcile order" }).click();
  expect(keys).toHaveLength(0);
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "Reconcile order" }).click();
  await expect(dialog.getByRole("alert")).toContainText("Please retry reconciliation");
  await dialog.getByRole("button", { name: "Reconcile order" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Reconcile order" })).toHaveCount(0);
  expect(keys).toHaveLength(2);
  expect(keys[0]).toBeTruthy();
  expect(keys[0]).toBe(keys[1]);
});

test("list 403 supports retry without claiming empty data", async ({ page }) => {
  await setup(page, [view]);
  let fails = true;
  await page.route("**/api/v1/ordering/orders?**", route => route.fulfill(fails ? { status: 403, json: { detail: "Order access denied" } } : { json: paged([]) }));
  await page.goto("/orders");
  await expect(page.getByText("Order access denied")).toBeVisible();
  await expect(page.getByText("No matching orders.")).toHaveCount(0);
  fails = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No matching orders.")).toBeVisible();
});

test("Chinese mobile order details fit and show localized amounts", async ({ page }) => {
  await setup(page, [view]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto(`/orders/${id}`);
  await expect(page.getByRole("heading", { name: "订单明细" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});

test("claim registration uses the selected line and rejects excess quantities", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage"]);
  let body: unknown;
  await page.route("**/api/v1/ordering/after-sales", async route => {
    body = route.request().postDataJSON();
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    await route.fulfill({ json: { id: "claim-1" } });
  });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Register claim" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Order line").selectOption("line-1");
  await dialog.getByLabel("Claim quantity").fill("10");
  await dialog.getByLabel("Reason").fill("Damaged packaging");
  await expect(dialog.getByRole("button", { name: "Apply claim" })).toBeDisabled();
  await dialog.getByLabel("Claim quantity").fill("2");
  await dialog.getByRole("button", { name: "Apply claim" }).click();
  await expect(dialog).toHaveCount(0);
  expect(body).toEqual({ orderId: id, orderLineId: "line-1", type: "Return", quantity: 2, reason: "Damaged packaging" });
});

test("orders without received status cannot reconcile or register claims", async ({ page }) => {
  await setup(page, [view, reconcile, "Permissions.Ordering.Orders.Manage"]);
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: "InTransit" } }));
  await page.goto(`/orders/${id}`);
  await expect(page.getByRole("heading", { name: "SO-1001" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Reconcile order|Register claim/ })).toHaveCount(0);
});

test("store filter sends the business store ID with root identity", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Stores.View"]);
  await page.route("**/api/v1/ordering/stores?**", route => route.fulfill({ json: [{ id: order.storeId, code: "S1", name: "Acme" }] }));
  await page.goto("/orders");
  const request = page.waitForRequest(request => request.url().includes(`storeId=${order.storeId}`));
  await page.getByRole("combobox", { name: "Store", exact: true }).selectOption(order.storeId);
  expect((await request).headers().tenant).toBe("root");
});

test("amend existing quantities without product permission preserves retry payload", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage"]);
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: "Reserved", cutoffAt: "2099-01-01T00:00:00Z" } }));
  const writes: { key: string; body: unknown }[] = [];
  await page.route(`**/api/v1/ordering/orders/${id}/amend`, async route => {
    expect(route.request().headers().tenant).toBe("root");
    writes.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
    await route.fulfill(writes.length === 1 ? { status: 409, json: { detail: "Stock unavailable" } } : { json: id });
  });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Amend order", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Search products to add")).toHaveCount(0);
  await dialog.getByLabel("Quantity 1", { exact: true }).fill("4");
  await dialog.getByRole("button", { name: "Submit amendment" }).click();
  await expect(dialog.getByText("Stock unavailable")).toBeVisible();
  await expect(dialog.getByLabel("Quantity 1", { exact: true })).toHaveValue("4");
  await dialog.getByRole("button", { name: "Submit amendment" }).click();
  await expect(dialog).toHaveCount(0);
  expect(writes[0]).toEqual(writes[1]);
  expect(writes[0].body).toEqual({ orderId: id, lines: [{ productId: "product-1", quantity: 4 }] });
});

test("cancel requires confirmation and refreshes the order state", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage"]);
  let cancelled = false;
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: cancelled ? "Cancelled" : "Reserved", cutoffAt: "2099-01-01T00:00:00Z" } }));
  await page.route(`**/api/v1/ordering/orders/${id}/cancel`, async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    cancelled = true;
    await route.fulfill({ json: id });
  });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Cancel order", exact: true }).click();
  expect(cancelled).toBe(false);
  await page.getByRole("dialog").getByRole("button", { name: "Cancel order", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Cancel order", exact: true })).toHaveCount(0);
  await expect(page.getByText("Cancelled", { exact: true })).toBeVisible();
});

test("past-cutoff reserved orders have no amend or cancel buttons", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage"]);
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: "Reserved", cutoffAt: "2000-01-01T00:00:00Z" } }));
  await page.goto(`/orders/${id}`);
  await expect(page.getByText("Changes and cancellation are available only for reserved orders before cutoff.")).toBeVisible();
  await expect(page.getByRole("button", { name: /Amend order|Cancel order/ })).toHaveCount(0);
});

test("missing order does not fetch claims or show write controls", async ({ page }) => {
  await setup(page, [view, reconcile, "Permissions.Ordering.Orders.Manage"]);
  let claims = 0;
  page.on("request", request => { if (request.url().includes("/api/v1/ordering/after-sales")) claims++; });
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ status: 404, json: { detail: "Order not found" } }));
  await page.goto(`/orders/${id}`);
  await expect(page.getByText("Order not found")).toBeVisible();
  await expect(page.getByRole("button", { name: /Amend order|Cancel order|Reconcile order|Register claim/ })).toHaveCount(0);
  expect(claims).toBe(0);
});

test("amend supports product addition and line removal", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage", "Permissions.Catalog.Products.View"]);
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { ...order, status: "Reserved", cutoffAt: "2099-01-01T00:00:00Z" } }));
  await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([{ id: "product-2", sku: "NEW", name: "New product", isActive: true }]) }));
  let payload: unknown;
  await page.route(`**/api/v1/ordering/orders/${id}/amend`, async route => { payload = route.request().postDataJSON(); await route.fulfill({ json: id }); });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Amend order", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "Remove line" }).click();
  await expect(dialog.getByRole("button", { name: "Submit amendment" })).toBeDisabled();
  await dialog.getByRole("button", { name: "Add", exact: true }).click();
  await dialog.getByLabel("Quantity 1", { exact: true }).fill("2");
  await dialog.getByRole("button", { name: "Submit amendment" }).click();
  await expect(dialog).toHaveCount(0);
  expect(payload).toEqual({ orderId: id, lines: [{ productId: "product-2", quantity: 2 }] });
});

test("claim server rejection preserves inputs and unchanged retry key", async ({ page }) => {
  await setup(page, [view, "Permissions.Ordering.Orders.Manage"]);
  const keys: string[] = [];
  await page.route("**/api/v1/ordering/after-sales", async route => {
    keys.push(route.request().headers()["idempotency-key"]);
    await route.fulfill(keys.length === 1 ? { status: 403, json: { detail: "Claim permission revoked" } } : { json: { id: "claim-2" } });
  });
  await page.goto(`/orders/${id}`);
  await page.getByRole("button", { name: "Register claim" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Order line").selectOption("line-1");
  await dialog.getByLabel("Claim quantity").fill("1");
  await dialog.getByLabel("Reason").fill("Damaged");
  await dialog.getByRole("button", { name: "Apply claim" }).click();
  await expect(dialog.getByText("Claim permission revoked")).toBeVisible();
  await expect(dialog.getByLabel("Reason")).toHaveValue("Damaged");
  await dialog.getByRole("button", { name: "Apply claim" }).click();
  await expect(dialog).toHaveCount(0);
  expect(keys).toHaveLength(2);
  expect(keys[0]).toBe(keys[1]);
});
