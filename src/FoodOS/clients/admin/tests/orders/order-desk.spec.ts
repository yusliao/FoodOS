import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const deskView = "Permissions.Ordering.OrderDesk.View";
const deskPlace = "Permissions.Ordering.OrderDesk.Place";
const ordersView = "Permissions.Ordering.Orders.View";
const storeId = "11111111-1111-1111-1111-111111111111";
const salmonId = "44444444-4444-4444-4444-444444444444";
const shrimpId = "55555555-5555-5555-5555-555555555555";
const orderId = "77777777-7777-7777-7777-777777777777";

const caller = {
  storeId, storeCode: "HB-DT", storeName: "Harbor Downtown", address: "100 Atlantic Ave", deliveryWindow: "05:00-08:00",
  customerOrgId: "22222222-2222-2222-2222-222222222222", customerOrgCode: "HARBOR", customerOrgName: "Harbor Group",
  creditHold: false, canPlaceOrders: true, blockingReason: null,
  contacts: [{ id: "c-1", name: "Front desk", phone: "617-555-0101" }], lastOrderAt: "2026-10-27T05:00:00Z",
};
const salmon = {
  productId: salmonId, sku: "SAL-001", name: "三文鱼刺身", baseUom: "KG", catchWeight: false, thumbnailUrl: null,
  unitPrice: 8, currency: "USD", priceSource: "Contract", isAvailable: false, availabilityStatus: "notConfigured",
};
const history = {
  recentOrders: [{
    id: "66666666-6666-6666-6666-666666666666", number: "SO20261027001", status: "Reserved", businessDate: "2026-10-27",
    placedAt: "2026-10-27T05:00:00Z", cutoffAt: "2026-10-27T14:00:00Z", revision: 0, placementChannel: "OrderDesk",
    warehouseConfirmationStatus: "Pending", totalAmount: 21, currency: "USD",
    lines: [{ lineId: "l-1", productId: shrimpId, sku: "SHR-001", name: "Peeled shrimp", baseUom: "KG", orderedQty: 3, shortageQty: 0, unitPrice: 7, currency: "USD" }],
  }],
  frequentProducts: [{ productId: shrimpId, sku: "SHR-001", name: "Peeled shrimp", baseUom: "KG", isActive: true, orderCount: 4, totalQty: 12, lastQty: 3, lastOrderedAt: "2026-10-27T05:00:00Z" }],
};

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/ordering/order-desk/callers**", route => route.fulfill({ json: [caller] }));
  await page.route("**/api/v1/ordering/order-desk/callers/mine**", route => route.fulfill({ json: [] }));
  await page.route("**/api/v1/ordering/order-desk/stores/*/products**", route => route.fulfill({ json: [salmon] }));
  await page.route("**/api/v1/ordering/order-desk/stores/*/history**", route => route.fulfill({ json: history }));
  await page.route("**/api/v1/ordering/order-desk/stores/*/quote", async route => {
    const body = route.request().postDataJSON() as { lines: { productId: string; quantity: number }[] };
    await route.fulfill({ json: body.lines.map(line => {
      const unitPrice = line.productId === salmonId ? 8 : 7;
      return { ...line, unitPrice, currency: "USD", priceSource: "Contract", lineTotal: unitPrice * line.quantity };
    }) });
  });
}

test("customer service chooses the customer, adds products by pinyin and frequent items, and places the order", async ({ page }) => {
  await setup(page, [deskView, deskPlace, ordersView]);
  const placements: { key: string | undefined; body: unknown }[] = [];
  await page.route("**/api/v1/ordering/order-desk/stores/*/orders", async route => {
    placements.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
    await route.fulfill({ json: orderId });
  });

  await page.goto("/order-desk");
  const lookup = page.getByRole("textbox", { name: "Find a customer" });
  await expect(lookup).toBeFocused();
  const callerRequest = page.waitForRequest(request => request.url().includes("/order-desk/callers?term=6175550101"));
  await lookup.fill("6175550101");
  await callerRequest;
  await page.getByRole("button", { name: /Harbor Downtown/ }).click();

  await expect(page.getByText("Harbor Group")).toBeVisible();
  await expect(page.getByRole("link", { name: "SO20261027001" })).toBeVisible();
  await expect(page.getByText("Service order", { exact: true })).toBeVisible();

  const products = page.getByRole("combobox", { name: "Search products" });
  await products.fill("swy");
  await expect(page.getByRole("option", { name: /三文鱼刺身/ })).toBeVisible();
  await products.press("Enter");
  await expect(page.getByRole("spinbutton", { name: "Quantity 三文鱼刺身" })).toHaveValue("1");
  await page.getByRole("button", { name: "Increase quantity" }).click();

  await page.getByRole("button", { name: /Peeled shrimp × 3/ }).click();
  await expect(page.getByRole("spinbutton", { name: "Quantity Peeled shrimp" })).toHaveValue("3");
  await expect(page.getByText("$37.00")).toBeVisible();

  await page.getByRole("button", { name: "Place order" }).click();
  await expect(page.getByRole("link", { name: "View order" })).toHaveAttribute("href", `/orders/${orderId}`);
  expect(placements).toHaveLength(1);
  expect(placements[0].key).toMatch(/^[0-9a-f-]{36}$/);
  expect(placements[0].body).toEqual({ lines: [{ productId: salmonId, quantity: 2 }, { productId: shrimpId, quantity: 3 }] });
  await expect(page.getByText("Search a product, pick a frequent item or copy a recent order.")).toBeVisible();
});

test("the agent's frequent customers show level and order count and open the desk in one click", async ({ page }) => {
  await setup(page, [deskView, deskPlace]);
  const vip = { ...caller, levelCode: "VIP", levelName: "VIP", levelDiscountPercent: 10 };
  await page.route("**/api/v1/ordering/order-desk/callers/mine**", route => route.fulfill({ json: [{ caller: vip, orderCount: 7 }] }));
  await page.goto("/order-desk");
  const mine = page.getByRole("region", { name: "My frequent customers" });
  const card = mine.getByRole("button", { name: /Harbor Group/ });
  await expect(card).toContainText("VIP · Pay 90%");
  await expect(card).toContainText("7 orders");
  await card.click();
  const selected = page.getByRole("region", { name: "Choose customer" });
  await expect(selected).toContainText("Discount level");
  await expect(selected).toContainText("VIP · Pay 90%");
});

test("a customer without a level is shown at the standard price", async ({ page }) => {
  await setup(page, [deskView]);
  await page.goto("/order-desk");
  await expect(page.getByText("You have not placed service orders in the last 90 days.")).toBeVisible();
  await page.getByRole("textbox", { name: "Find a customer" }).fill("Harbor");
  await page.getByRole("button", { name: /Harbor Group/ }).click();
  await expect(page.getByRole("region", { name: "Choose customer" })).toContainText("Standard price (no level)");
});

test("a view-only agent can prepare lines but has no place action", async ({ page }) => {
  await setup(page, [deskView]);
  await page.goto("/order-desk");
  await page.getByRole("textbox", { name: "Find a customer" }).fill("HB-DT");
  await page.getByRole("button", { name: /Harbor Downtown/ }).click();
  await page.getByRole("button", { name: "Add these lines" }).click();
  await expect(page.getByRole("spinbutton", { name: "Quantity Peeled shrimp" })).toHaveValue("3");
  await expect(page.getByRole("button", { name: "Place order" })).toHaveCount(0);
  await expect(page.getByText("You can prepare lines but cannot place service orders.")).toBeVisible();
});

test("the order desk is hidden and blocked without the order desk permission", async ({ page }) => {
  await setup(page, [ordersView]);
  const deskRequests: string[] = [];
  page.on("request", request => { if (request.url().includes("/order-desk/")) deskRequests.push(request.url()); });
  await page.goto("/order-desk");
  await expect(page.getByRole("link", { name: "Service orders" })).toHaveCount(0);
  await expect(page.getByRole("textbox", { name: "Find a customer" })).toHaveCount(0);
  expect(deskRequests).toEqual([]);
});
