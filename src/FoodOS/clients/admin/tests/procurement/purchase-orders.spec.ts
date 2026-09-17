import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, WMS_NOT_CONFIGURED } from "../helpers/shell-mocks";

const view = "Permissions.Procurement.Purchase.View";
const create = "Permissions.Procurement.Purchase.Create";
const order = { id: "po-1", number: "PO20260917001", supplierId: "supplier-1", warehouseId: "warehouse-1", status: "Draft", expectedAt: "2026-09-18T10:00:00Z", appointment: null, qualityChecks: [], lines: [{ id: "line-1", productId: "product-1", zone: "Ambient", quantity: 10, receivedQty: 0, rejectedQty: 0 }] };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [order] }));
}

test("purchase reader sees lines without write controls or unauthorized lookups", async ({ page }) => {
  await setup(page, [view]);
  const requests: string[] = [];
  page.on("request", request => requests.push(request.url()));
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: order.number })).toBeVisible();
  await expect(page.getByRole("button", { name: /Mark as sent|Book inbound appointment|Create purchase order/ })).toHaveCount(0);
  expect(requests.some(url => /api\/v1\/(inventory|catalog)|procurement\/suppliers/.test(url))).toBe(false);
});

test("purchase URL is forbidden without view permission", async ({ page }) => {
  await setup(page, [create]);
  let requests = 0;
  page.on("request", request => { if (request.url().includes("/api/v1/procurement")) requests++; });
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toBe(0);
});

test("sending a draft confirms the operation and refreshes status", async ({ page }) => {
  await setup(page, [view, create]);
  let sent = false;
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ ...order, status: sent ? "Sent" : "Draft" }] }));
  await page.route("**/api/v1/procurement/purchase-orders/po-1/send", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    sent = true;
    await route.fulfill({ json: order.id });
  });
  await page.goto("/procurement/purchase-orders");
  await page.getByRole("button", { name: "Mark as sent" }).click();
  expect(sent).toBe(false);
  await page.getByRole("dialog").getByRole("button", { name: "Mark as sent" }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Mark as sent" })).toHaveCount(0);
});

test("appointment conflict preserves form and retry key", async ({ page }) => {
  await setup(page, [view, create]);
  const keys: string[] = [];
  let appointment: null | { id: string; dockSlot: string; vehicleNo: string } = null;
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ ...order, appointment, status: appointment ? "Receiving" : "Sent" }] }));
  await page.route("**/api/v1/procurement/purchase-orders/po-1/appointments", async route => {
    keys.push(route.request().headers()["idempotency-key"]);
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ purchaseOrderId: order.id, dockSlot: "A1", vehicleNo: "TRUCK-1" });
    if (keys.length > 1) appointment = { id: "appointment-1", dockSlot: "A1", vehicleNo: "TRUCK-1" };
    await route.fulfill(appointment ? { json: appointment.id } : { status: 409, json: { detail: "Appointment conflict" } });
  });
  await page.goto("/procurement/purchase-orders");
  await page.getByRole("button", { name: "Book inbound appointment" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Dock slot").fill("A1");
  await dialog.getByLabel("Vehicle number").fill("TRUCK-1");
  await dialog.getByRole("button", { name: "Book inbound appointment" }).click();
  await expect(dialog.getByText("Appointment conflict")).toBeVisible();
  await expect(dialog.getByLabel("Dock slot")).toHaveValue("A1");
  await dialog.getByRole("button", { name: "Book inbound appointment" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Book inbound appointment" })).toHaveCount(0);
  expect(keys[0]).toBeTruthy();
  expect(keys[0]).toBe(keys[1]);
});

test("failed purchase listing retries into empty data", async ({ page }) => {
  await setup(page, [view]);
  let denied = true;
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill(denied ? { status: 403, json: { detail: "Purchase access denied" } } : { json: [] }));
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByText("Purchase access denied")).toBeVisible();
  await expect(page.getByText("No matching purchase orders.")).toHaveCount(0);
  denied = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No matching purchase orders.")).toBeVisible();
});

test("Chinese purchase cards fit a narrow screen", async ({ page }) => {
  await setup(page, [view]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: "采购单工作台" })).toBeVisible();
  await expect(page.getByRole("heading", { name: order.number })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});

for (const [result, permission] of [["pass", "Permissions.Procurement.Quality.Pass"], ["fail", "Permissions.Procurement.Quality.Fail"]]) {
  test(`${result} permission never enables local QC in external WMS mode`, async ({ page }) => {
    await setup(page, [view, permission]);
    await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ ...order, status: "Receiving" }] }));
    const writes: string[] = [];
    page.on("request", request => { if (request.method() === "POST") writes.push(request.url()); });
    await page.goto("/procurement/purchase-orders");
    await expect(page.getByRole("heading", { name: order.number })).toBeVisible();
    await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
    await expect(page.getByRole("button", { name: /Pass quality check|Fail quality check/ })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Mark as sent|Book inbound appointment/ })).toHaveCount(0);
    expect(writes).toEqual([]);
  });
}

test("purchase creator cannot perform QC while receiving", async ({ page }) => {
  await setup(page, [view, create]);
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ ...order, status: "Receiving" }] }));
  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: order.number })).toBeVisible();
  await expect(page.getByRole("button", { name: /Pass quality check|Fail quality check/ })).toHaveCount(0);
});

for (const result of ["pass", "fail"]) {
  test(result + " QC remains disabled through capability loading, failure and retry", async ({ page }) => {
    await setup(page, [view, "Permissions.Procurement.Quality." + (result === "pass" ? "Pass" : "Fail")]);
    await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [{ ...order, status: "Receiving" }] }));
    let failed = true;
    let release: (() => void) | undefined;
    await page.route("**/api/v1/fulfillment/capabilities", async route => {
      expect(route.request().headers().tenant).toBe("root");
      if (failed) await new Promise<void>(resolve => { release = resolve; });
      await route.fulfill(failed ? { status: 403, json: {} } : { json: WMS_NOT_CONFIGURED });
    });
    const writes: string[] = [];
    page.on("request", request => { if (request.method() === "POST") writes.push(request.url()); });
    await page.goto("/procurement/purchase-orders");
    await expect.poll(() => !!release).toBe(true);
    await expect(page.getByRole("heading", { name: order.number })).toBeVisible();
    await expect(page.getByRole("button", { name: /Pass quality check|Fail quality check/ })).toHaveCount(0);
    release?.();
    await expect(page.getByText("WMS status could not be verified. Execution remains disabled.")).toBeVisible();
    failed = false;
    await page.getByRole("button", { name: "Retry WMS status" }).click();
    await expect(page.getByText("WMS integration is not ready. Stock and delivery cannot be confirmed.")).toBeVisible();
    await expect(page.getByRole("button", { name: /Pass quality check|Fail quality check/ })).toHaveCount(0);
    expect(writes).toEqual([]);
  });
}
