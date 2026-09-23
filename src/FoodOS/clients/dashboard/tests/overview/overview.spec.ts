import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { installShellMocks, paged } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

const SHOP_PERMISSIONS = [
  "Permissions.Ordering.Shop.View",
  "Permissions.Ordering.Shop.Order",
];

const STORE = {
  id: "11111111-1111-1111-1111-111111111111",
  code: "S000001",
  name: "Harbor Kitchen",
  address: "12 Dock St",
  deliveryWindow: "05:00-08:00",
};

const SECOND_STORE = {
  id: "99999999-9999-9999-9999-999999999999",
  code: "S000002",
  name: "Uptown Kitchen",
  address: "88 Hill St",
  deliveryWindow: "06:00-09:00",
};

const RECENT_ORDER = {
  id: "77777777-7777-7777-7777-777777777777",
  number: "SO202609230001",
  storeId: STORE.id,
  status: "Reserved",
  businessDate: "2026-09-23",
  cutoffAt: "2026-09-23T08:00:00Z",
  placedAt: "2026-09-23T01:00:00Z",
  revision: 1,
  lines: [],
};

async function mockCustomerHome(page: import("@playwright/test").Page, orders = [RECENT_ORDER]) {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", SHOP_PERMISSIONS);
  await mockJsonResponse(page, "**/api/v1/shop/stores**", [STORE]);
  await mockJsonResponse(page, `**/api/v1/shop/stores/${STORE.id}/cart`, {
    id: "cart-1",
    storeId: STORE.id,
    lines: [{ productId: "product-1", quantity: 2 }],
    updatedAt: "2026-09-23T01:00:00Z",
  });
  await mockJsonResponse(page, "**/api/v1/shop/orders**", paged(orders));
}

test.describe("overview (/)", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, TEST_USER);
    await installShellMocks(page);
  });

  test("centres the authorized restaurant store, cart and recent orders without operator APIs", async ({ page }) => {
    await mockCustomerHome(page);
    const operatorRequests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/(billing|audits|catalog|inventory|ordering)(\/|\?|$)/.test(request.url())) {
        operatorRequests.push(request.url());
      }
    });

    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Welcome, Alice Nguyen" })).toBeVisible();
    await expect(page.getByText("Harbor Kitchen", { exact: true }).first()).toBeVisible();
    await expect(page.getByText("SO202609230001", { exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: /browse products/i })).toHaveAttribute("href", "/shop/catalog");
    await expect(page.getByRole("link", { name: /review cart/i })).toHaveAttribute("href", "/shop/cart");
    expect(operatorRequests).toEqual([]);
  });

  test("does not call Shop APIs when the account lacks Shop.View", async ({ page }) => {
    const shopRequests: string[] = [];
    page.on("request", (request) => {
      if (request.url().includes("/api/v1/shop/")) shopRequests.push(request.url());
    });

    await page.goto("/");
    await expect(page.getByRole("heading", { name: /ordering access isn't available/i })).toBeVisible();
    expect(shopRequests).toEqual([]);
  });

  test("retains the store error until an explicit retry succeeds", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/identity/permissions", SHOP_PERMISSIONS);
    let ready = false;
    await page.route("**/api/v1/shop/stores**", async (route) => {
      await route.fulfill({
        status: ready ? 200 : 503,
        contentType: "application/json",
        body: JSON.stringify(ready ? [STORE] : { detail: "Store service unavailable" }),
      });
    });
    await mockJsonResponse(page, `**/api/v1/shop/stores/${STORE.id}/cart`, {
      id: "cart-1",
      storeId: STORE.id,
      lines: [],
      updatedAt: "2026-09-23T01:00:00Z",
    });
    await mockJsonResponse(page, "**/api/v1/shop/orders**", paged([]));

    await page.goto("/");
    await expect(page.getByRole("alert")).toContainText("Store service unavailable");
    ready = true;
    await page.getByRole("button", { name: /retry stores/i }).click();
    await expect(page.getByText("Harbor Kitchen", { exact: true }).first()).toBeVisible();
  });

  test("renders the Chinese empty-order state without mobile overflow", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
    await mockCustomerHome(page, []);

    await page.goto("/");
    await expect(page.getByRole("heading", { name: "欢迎，Alice Nguyen" })).toBeVisible();
    await expect(page.getByText("暂无订单", { exact: true })).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });

  test("replaces a stale stored store with the first authorized store without requesting the stale scope", async ({ page }) => {
    const staleStoreId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    await page.addInitScript(
      ({ key, value }) => localStorage.setItem(key, value),
      { key: "foodos.shop.storeId", value: staleStoreId },
    );
    await mockCustomerHome(page, []);
    const scopedRequests: string[] = [];
    page.on("request", (request) => {
      if (request.url().includes("/api/v1/shop/")) scopedRequests.push(request.url());
    });

    await page.goto("/");
    await expect(page.getByText("Harbor Kitchen", { exact: true }).first()).toBeVisible();
    await expect.poll(() => page.evaluate(() => localStorage.getItem("foodos.shop.storeId"))).toBe(STORE.id);
    expect(scopedRequests.some((url) => url.includes(staleStoreId))).toBe(false);
  });

  test("persists an authorized store switch and scopes subsequent customer requests to it", async ({ page }) => {
    await page.addInitScript(
      ({ key, value }) => localStorage.setItem(key, value),
      { key: "foodos.shop.storeId", value: STORE.id },
    );
    await mockJsonResponse(page, "**/api/v1/identity/permissions", SHOP_PERMISSIONS);
    await mockJsonResponse(page, "**/api/v1/shop/stores**", [STORE, SECOND_STORE]);
    await mockJsonResponse(page, "**/api/v1/shop/stores/*/cart", {
      id: "cart-1",
      storeId: SECOND_STORE.id,
      lines: [],
      updatedAt: "2026-09-23T01:00:00Z",
    });
    const requestedStoreIds: string[] = [];
    await page.route("**/api/v1/shop/orders**", async (route) => {
      requestedStoreIds.push(new URL(route.request().url()).searchParams.get("storeId") ?? "");
      await route.fulfill({ json: paged([]) });
    });

    await page.goto("/");
    await page.getByRole("button", { name: "Store", exact: true }).click();
    await page.getByRole("menuitemradio", { name: /Uptown Kitchen/ }).click();

    await expect(page.getByText("Uptown Kitchen", { exact: true }).first()).toBeVisible();
    await expect.poll(() => page.evaluate(() => localStorage.getItem("foodos.shop.storeId"))).toBe(SECOND_STORE.id);
    await expect.poll(() => requestedStoreIds.at(-1)).toBe(SECOND_STORE.id);

    await page.getByRole("button", { name: "Store", exact: true }).click();
    await page.getByRole("menuitemradio", { name: /Harbor Kitchen/ }).click();
    await expect.poll(() => requestedStoreIds.filter((id) => id === STORE.id).length).toBeGreaterThan(1);
  });
});

test.describe("retired tenant dashboard routes", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, TEST_USER);
    await installShellMocks(page);
  });

  for (const path of ["/activity", "/invoices"]) {
    test(`shows the explicit retired result for ${path}`, async ({ page }) => {
      const requests: string[] = [];
      page.on("request", (request) => {
        if (/\/api\/v1\/(billing|activity)/.test(request.url())) requests.push(request.url());
      });

      await page.goto(path);

      await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
      await expect(page.getByText(`Retired route: ${path}`)).toBeVisible();
      expect(requests).toEqual([]);
    });
  }
});
