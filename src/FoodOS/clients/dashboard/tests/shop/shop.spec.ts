import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installShellMocks, paged } from "../helpers/shell-mocks";

const STORE = {
  id: "11111111-1111-1111-1111-111111111111",
  code: "S000001",
  name: "Harbor Kitchen",
  address: "12 Dock St",
  deliveryWindow: "05:00-08:00",
};

const PRODUCT = {
  id: "44444444-4444-4444-4444-444444444444",
  sku: "SAL-001",
  name: "Atlantic Salmon",
  description: "Chilled side.",
  brandId: "55555555-5555-5555-5555-555555555555",
  categoryId: "66666666-6666-6666-6666-666666666666",
  unitPrice: 8,
  currency: "USD",
  priceSource: "Contract",
  baseUom: "lb",
  catchWeight: false,
  thumbnailUrl: null,
  isAvailable: true,
};

const ORDER_ID = "77777777-7777-7777-7777-777777777777";

const SHOP_PERMS = [
  "Permissions.Ordering.Shop.View",
  "Permissions.Ordering.Shop.Order",
];

function reservedOrder(over: Record<string, unknown> = {}) {
  return {
    id: ORDER_ID,
    number: "SO202609110001",
    storeId: STORE.id,
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

async function mockShopApis(
  page: Page,
  options: { available?: number; cartLines?: unknown[]; permissions?: string[] } = {},
) {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", options.permissions ?? SHOP_PERMS);
  await mockJsonResponse(page, "**/api/v1/shop/stores**", [STORE]);
  await page.route("**/api/v1/shop/products**", async (route) => {
    const url = new URL(route.request().url());
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(
        url.pathname.endsWith(`/${PRODUCT.id}`)
          ? { ...PRODUCT, isAvailable: (options.available ?? 20) > 0 }
          : paged([{ ...PRODUCT, isAvailable: (options.available ?? 20) > 0 }]),
      ),
    });
  });
  await mockJsonResponse(page, "**/api/v1/shop/stores/*/cart", {
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
    const operatorRequests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/(catalog|inventory|ordering)\//.test(request.url())) {
        operatorRequests.push(request.url());
      }
    });
    await page.goto("/shop/catalog");

    await expect(page.getByRole("heading", { name: /order catalog/i })).toBeVisible();
    await expect(page.getByText("Atlantic Salmon").last()).toBeVisible();
    await expect(page.getByTestId("quoted-price").last()).toHaveText(/\$8\.00/);
    await expect(page.getByText("Contract").last()).toBeVisible();
    expect(operatorRequests).toEqual([]);
  });

  test("disables add when the customer product is unavailable", async ({ page }) => {
    await mockShopApis(page, { available: 0 });
    await page.goto("/shop/catalog");

    await expect(page.getByText("Out of stock").last()).toBeVisible();
    await expect(page.getByRole("button", { name: /^add/i }).last()).toBeDisabled();
  });

  test("adds the quoted SKU to the cart", async ({ page }) => {
    await mockShopApis(page);
    let putBody: unknown;
    await page.route("**/api/v1/shop/stores/*/cart", async (route) => {
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
      lines: [{ productId: PRODUCT.id, quantity: 1 }],
    });
  });

  test("lets a read-only customer browse without requesting or changing a cart", async ({ page }) => {
    await mockShopApis(page, { permissions: ["Permissions.Ordering.Shop.View"] });
    const cartRequests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/shop\/stores\/[^/]+\/cart/.test(request.url())) {
        cartRequests.push(request.url());
      }
    });

    await page.goto("/shop/catalog");
    await expect(page.getByText(PRODUCT.name).last()).toBeVisible();
    await expect(page.getByRole("button", { name: /^add/i }).last()).toBeDisabled();
    await expect(page.getByRole("link", { name: /^cart/i })).toHaveCount(0);
    expect(cartRequests).toEqual([]);
  });

  test("blocks a direct shop URL before any customer request when Shop.View is absent", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/identity/permissions", []);
    const shopRequests: string[] = [];
    page.on("request", (request) => {
      if (request.url().includes("/api/v1/shop/")) shopRequests.push(request.url());
    });

    await page.goto("/shop/catalog");
    await expect(page.getByText("Shop access required", { exact: true })).toBeVisible();
    expect(shopRequests).toEqual([]);
  });
});

test.describe("shop/orders", () => {
  test("hides amend and cancel after cutoff", async ({ page }) => {
    await mockShopApis(page);
    const order = reservedOrder({
      status: "Planned",
      cutoffAt: new Date(Date.now() - 60_000).toISOString(),
    });
    await mockJsonResponse(page, `**/api/v1/shop/orders/${ORDER_ID}`, order);

    await page.goto(`/shop/orders/${ORDER_ID}`);
    await expect(page.getByText("This order is locked. Changes are no longer allowed.")).toBeVisible();
    await expect(page.getByRole("button", { name: /save changes/i })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /cancel order/i })).toHaveCount(0);
  });
});

test.describe("shop/cart", () => {
  test("requests the customer price for the cart quantity without operator quote APIs", async ({ page }) => {
    await mockShopApis(page, {
      cartLines: [{ productId: PRODUCT.id, quantity: 3 }],
    });
    const productRequests: string[] = [];
    const operatorRequests: string[] = [];
    page.on("request", (request) => {
      if (request.url().includes(`/api/v1/shop/products/${PRODUCT.id}`)) {
        productRequests.push(request.url());
      }
      if (/\/api\/v1\/(catalog|inventory|ordering)\//.test(request.url())) {
        operatorRequests.push(request.url());
      }
    });

    await page.goto("/shop/cart");
    await expect(page.getByText(PRODUCT.name)).toBeVisible();
    await expect.poll(() => productRequests.length).toBeGreaterThan(0);
    const requestUrl = new URL(productRequests.at(-1)!);
    expect(requestUrl.searchParams.get("storeId")).toBe(STORE.id);
    expect(requestUrl.searchParams.get("quantity")).toBe("3");
    expect(operatorRequests).toEqual([]);
  });

  test("blocks direct cart access before the cart request when Shop.Order is absent", async ({ page }) => {
    await mockShopApis(page, { permissions: ["Permissions.Ordering.Shop.View"] });
    const cartRequests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/shop\/stores\/[^/]+\/cart/.test(request.url())) {
        cartRequests.push(request.url());
      }
    });

    await page.goto("/shop/cart");
    await expect(page.getByText("Ordering access required", { exact: true })).toBeVisible();
    expect(cartRequests).toEqual([]);
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
    await mockJsonResponse(page, "**/api/v1/shop/orders**", paged([order]));
    await mockJsonResponse(page, "**/api/v1/shop/after-sales**", [], { method: "GET" });

    let filed: unknown;
    await page.route("**/api/v1/shop/after-sales", async (route) => {
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
