import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
const status = { mode: "externalWms", readiness: "notConfigured", acceptsOrders: false, acceptsOrderChanges: false, localWarehouseExecution: false };
test("QC stays unavailable through status failure and retry while procurement remains readable", async ({ page }) => {
  const permissions = ["Permissions.Procurement.Purchase.View", "Permissions.Procurement.Quality.Pass", "Permissions.Procurement.Quality.Fail"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  let failed = true;
  await page.route("**/api/v1/fulfillment/capabilities", route => {
    expect(route.request().headers().tenant).toBe("root");
    return route.fulfill(failed ? { status: 403, json: {} } : { json: status });
  });
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ id: "po-1", number: "PO-1", status: "Receiving", supplierId: "s", warehouseId: "w", expectedAt: "2026-09-20T00:00:00Z", appointment: null, qualityChecks: [], lines: [{ id: "l", productId: "p", quantity: 5, receivedQty: 0, rejectedQty: 0, zone: "Ambient" }] }] }));
  const writes: string[] = [];
  page.on("request", request => { if (request.method() === "POST" && request.url().includes("/procurement/")) writes.push(request.url()); });
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: "PO-1", exact: true })).toBeVisible();
  await expect(page.getByText("WMS status could not be verified. Execution remains disabled.")).toBeVisible();
  await expect(page.getByRole("button", { name: /Pass quality check|Fail quality check/ })).toHaveCount(0);
  failed = false;
  await page.getByRole("button", { name: "Retry WMS status" }).click();
  await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
  expect(writes).toEqual([]);
});

test("order manager cannot amend or cancel while WMS is unavailable", async ({ page }) => {
  const permissions = ["Permissions.Ordering.Orders.View", "Permissions.Ordering.Orders.Manage"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/fulfillment/capabilities", route => route.fulfill({ json: status }));
  const id = "11111111-1111-4111-8111-111111111111";
  await page.route(`**/api/v1/ordering/orders/${id}`, route => route.fulfill({ json: { id, number: "SO-1", status: "Reserved", businessDate: "2026-09-17", cutoffAt: "2099-01-01T00:00:00Z", storeId: "s", warehouseId: "w", customerTenantId: "customer", lines: [] } }));
  await page.route("**/api/v1/ordering/after-sales**", route => route.fulfill({ json: [] }));
  await page.goto(`/orders/${id}`);
  await expect(page.getByText("SO-1", { exact: true })).toBeVisible();
  await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
  await expect(page.getByRole("button", { name: /Amend order|Cancel order/ })).toHaveCount(0);
});
