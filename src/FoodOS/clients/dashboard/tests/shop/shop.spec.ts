import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installShellMocks, paged } from "../helpers/shell-mocks";

const STORE = {
  id: "11111111-1111-1111-1111-111111111111",
  customerOrgId: "22222222-2222-2222-2222-222222222222",
  code: "S000001",
  name: "Harbor Kitchen",
  address: "12 Dock St",
  defaultWarehouseId: "33333333-3333-3333-3333-333333333333",
  defaultRouteId: null,
  deliveryWindow: "05:00-08:00",
  createdAtUtc: "2026-09-01T00:00:00Z",
};

const PRODUCT = {
  id: "44444444-4444-4444-4444-444444444444",
  sku: "SAL-001",
  name: "Atlantic Salmon",
  slug: "atlantic-salmon",
  description: "Chilled side.",
  brandId: "55555555-5555-5555-5555-555555555555",
  categoryId: "66666666-6666-6666-6666-666666666666",
  price: { amount: 199.99, currency: "USD" },
  stock: 42,
  isActive: true,
  temperatureZone: "Chilled",
  baseUom: "lb",
  thumbnailUrl: null,
  images: [],
  translations: [],
  createdAtUtc: "2026-09-01T00:00:00Z",
};

const ORDER_ID = "77777777-7777-7777-7777-777777777777";

const SHOP_PERMS = [
  "Permissions.Ordering.Shop.View",
  "Permissions.Ordering.Shop.Order",
  "Permissions.Catalog.Products.View",
  "Permissions.Inventory.Stock.View",
];

function quotedPrice(over: Record<string, unknown> = {}) {
  return {
    customerOrgId: STORE.customerOrgId,
    productId: PRODUCT.id,
    quantity: 1,
    unitPrice: 8,
    currency: "USD",
    source: "Contract",
    ...over,
  };
}

function reservedOrder(over: Record<string, unknown> = {}) {
  return {
    id: ORDER_ID,
    number: "SO202609110001",
    storeId: STORE.id,
    customerOrgId: STORE.customerOrgId,
    warehouseId: STORE.defaultWarehouseId,
    status: "Reserved",
    businessDate: "2026-09-11",
    cutoffAt: new Date(Date.now() + 4 * 60 * 60 * 1000).toISOString(),
    placedAt: new Date().toISOString(),
    revision: 1,
    lines: [
      {
        id: "88888888-8888-8888-8888-888888888888",
        productId: PRODUCT.id,
        zone: "Chilled",
        orderedQty: 2,
        reservedQty: 2,
        deliveredQty: 0,
        returnedQty: 0,
        shortageQty: 0,
        shortageReason: null,
        varianceReason: null,
        unitPrice: 8,
        currency: "USD",
        reservationId: "99999999-9999-9999-9999-999999999999",
        lots: [],
      },
    ],
    ...over,
  };
}

async function mockShopApis(page: Page, options: { available?: number; cartLines?: unknown[] } = {}) {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", SHOP_PERMS);
  await mockJsonResponse(page, "**/api/v1/ordering/stores**", [STORE]);
  await mockJsonResponse(page, "**/api/v1/catalog/categories**", paged([]));
  await mockJsonResponse(page, "**/api/v1/catalog/products**", paged([PRODUCT]));
  await mockJsonResponse(page, `**/api/v1/catalog/products/${PRODUCT.id}`, PRODUCT);
  await page.route("**/api/v1/catalog/quotes**", async (route) => {
    const url = new URL(route.request().url());
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(
        quotedPrice({
          productId: url.searchParams.get("productId"),
          quantity: Number(url.searchParams.get("quantity") ?? 1),
        }),
      ),
    });
  });
  await mockJsonResponse(page, "**/api/v1/inventory/stock/available**", {
    warehouseId: STORE.defaultWarehouseId,
    productId: PRODUCT.id,
    available: options.available ?? 20,
    zoneKind: "Chilled",
  });
  await mockJsonResponse(page, "**/api/v1/ordering/carts/**", {
    id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    storeId: STORE.id,
    lines: options.cartLines ?? [],
    updatedAt: new Date().toISOString(),
  });
}

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await page.addInitScript(
    ({ key, storeId }) => {
      localStorage.setItem(key, storeId);
    },
    { key: "foodos.shop.storeId", storeId: STORE.id },
  );
  await installShellMocks(page);
});

test.describe("shop/catalog", () => {
  test("shows the quoted contract price and never the catalog list price", async ({ page }) => {
    await mockShopApis(page);
    await page.goto("/shop/catalog");

    await expect(page.getByRole("heading", { name: /order catalog/i })).toBeVisible();
    await expect(page.getByText("Atlantic Salmon").last()).toBeVisible();
    await expect(page.getByTestId("quoted-price").last()).toHaveText(/\$8\.00/);
    await expect(page.getByText("Contract").last()).toBeVisible();
    await expect(page.locator("body")).not.toContainText("199.99");
    await expect(page.locator("body")).not.toContainText("$199");
  });

  test("disables add when ATP is zero", async ({ page }) => {
    await mockShopApis(page, { available: 0 });
    await page.goto("/shop/catalog");

    await expect(page.getByText("Out of stock").last()).toBeVisible();
    await expect(page.getByRole("button", { name: /^add/i }).last()).toBeDisabled();
  });

  test("adds the quoted SKU to the cart", async ({ page }) => {
    await mockShopApis(page);
    let putBody: unknown;
    await page.route("**/api/v1/ordering/carts/**", async (route) => {
      if (route.request().method() !== "PUT") {
        await route.fallback();
        return;
      }
      putBody = JSON.parse(route.request().postData() ?? "{}");
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
      });
    });

    await page.goto("/shop/catalog");
    await page.getByRole("button", { name: /^add/i }).last().click();
    await expect.poll(() => putBody).toMatchObject({
      storeId: STORE.id,
      lines: [{ productId: PRODUCT.id, quantity: 1 }],
    });
  });
});

test.describe("shop/orders", () => {
  test("hides amend and cancel after cutoff", async ({ page }) => {
    await mockShopApis(page);
    const order = reservedOrder({
      status: "Planned",
      cutoffAt: new Date(Date.now() - 60_000).toISOString(),
    });
    await mockJsonResponse(page, `**/api/v1/ordering/orders/${ORDER_ID}`, order);

    await page.goto(`/shop/orders/${ORDER_ID}`);
    await expect(page.getByText("This order is locked. Changes are no longer allowed.")).toBeVisible();
    await expect(page.getByRole("button", { name: /save changes/i })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /cancel order/i })).toHaveCount(0);
  });
});

test.describe("shop/after-sales", () => {
  test("files a return claim against a received order", async ({ page }) => {
    await mockShopApis(page);
    const order = reservedOrder({
      status: "Received",
      lines: [
        {
          id: "88888888-8888-8888-8888-888888888888",
          productId: PRODUCT.id,
          zone: "Chilled",
          orderedQty: 6,
          reservedQty: 6,
          deliveredQty: 6,
          returnedQty: 0,
          shortageQty: 0,
          shortageReason: null,
          varianceReason: null,
          unitPrice: 8,
          currency: "USD",
          reservationId: "99999999-9999-9999-9999-999999999999",
          lots: [],
        },
      ],
    });
    await mockJsonResponse(page, "**/api/v1/ordering/orders**", paged([order]));
    await mockJsonResponse(page, "**/api/v1/ordering/after-sales**", [], { method: "GET" });

    let filed: unknown;
    await page.route("**/api/v1/ordering/after-sales", async (route) => {
      if (route.request().method() === "POST") {
        filed = JSON.parse(route.request().postData() ?? "{}");
        await route.fulfill({
          status: 200,
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            orderId: ORDER_ID,
            storeId: STORE.id,
            orderLineId: "88888888-8888-8888-8888-888888888888",
            type: "Return",
            quantity: 1,
            reason: "bruised",
            status: "Applied",
            createdByUserId: TEST_USER.sub,
            createdAt: new Date().toISOString(),
          }),
        });
        return;
      }
      await route.continue();
    });

    await page.goto("/shop/after-sales");
    await expect(page.getByRole("heading", { name: "After-sales" })).toBeVisible();

    await page.getByRole("button", { name: /choose a received order/i }).click();
    await page.getByRole("menuitemradio", { name: order.number }).click();
    await page.getByRole("button", { name: /choose a line/i }).click();
    await page.getByRole("menuitemradio", { name: PRODUCT.name }).click();
    await page.getByRole("button", { name: /^Shortage$/ }).click();
    await page.getByRole("menuitemradio", { name: /^Return$/ }).click();
    await page.getByLabel("Qty").fill("1");
    await page.getByLabel("Reason").fill("bruised");
    await page.getByRole("button", { name: /file a claim/i }).click();

    await expect.poll(() => filed).toMatchObject({
      orderId: ORDER_ID,
      orderLineId: "88888888-8888-8888-8888-888888888888",
      type: "Return",
      quantity: 1,
      reason: "bruised",
    });
  });
});
