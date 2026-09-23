import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { installShellMocks, paged } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

const INVOICE = {
  id: "inv-1",
  tenantId: "acme",
  invoiceNumber: "INV-2026-05",
  periodYear: 2026,
  periodMonth: 5,
  currency: "USD",
  subtotalAmount: 149,
  status: "Issued",
  createdAtUtc: "2026-05-01T00:00:00Z",
  issuedAtUtc: "2026-05-01T00:00:00Z",
  dueAtUtc: "2026-05-15T00:00:00Z",
  paidAtUtc: null,
  voidedAtUtc: null,
  notes: null,
  lineItems: [],
};

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
  });
});

test.describe("activity (/activity)", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, TEST_USER);
    await installShellMocks(page);
  });

  test("renders the live-activity page with its empty state (stream offline in tests)", async ({ page }) => {
    await page.goto("/activity");
    await expect(page.getByRole("heading", { name: /live activity/i })).toBeVisible();
    await expect(page.getByText(/no events yet|listening for activity/i)).toBeVisible();
  });
});

test.describe("invoices (/invoices)", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, TEST_USER);
    await installShellMocks(page);
  });

  test("renders an invoice row from the API", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/billing/invoices/me**", paged([INVOICE]));
    await page.goto("/invoices");
    await expect(page.getByRole("heading", { name: /invoices/i })).toBeVisible();
    // Invoice number + status render in both a (hidden) mobile card and the
    // desktop table row; the desktop one is last in the DOM on a wide viewport.
    await expect(page.getByText("INV-2026-05").last()).toBeVisible();
    await expect(page.getByText("Issued").last()).toBeVisible();
  });

  test("shows the empty state when there are no invoices", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/billing/invoices/me**", paged([]));
    await page.goto("/invoices");
    await expect(page.getByText(/no invoices yet/i)).toBeVisible();
  });

  test("filters by search term", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/billing/invoices/me**", paged([INVOICE]));
    await page.goto("/invoices");
    await expect(page.getByText("INV-2026-05").last()).toBeVisible();
    await page.getByPlaceholder(/search by invoice number/i).fill("nomatch-xyz");
    await expect(page.getByText(/no invoices found/i)).toBeVisible();
  });

  test("paginates across pages using the PagedResult envelope", async ({ page }) => {
    const PAGE_1 = { ...INVOICE, id: "inv-1", invoiceNumber: "INV-2026-05" };
    const PAGE_2 = { ...INVOICE, id: "inv-2", invoiceNumber: "INV-2026-04", periodMonth: 4 };

    // Serve page 1 or page 2 based on the requested pageNumber so the next
    // control drives a real envelope transition. totalCount=2, totalPages=2.
    await page.route("**/api/v1/billing/invoices/me**", async (route) => {
      const url = new URL(route.request().url());
      const pageNumber = Number(url.searchParams.get("pageNumber") ?? "1");
      const item = pageNumber >= 2 ? PAGE_2 : PAGE_1;
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(
          paged([item], { pageNumber, pageSize: 20, totalCount: 2, totalPages: 2 }),
        ),
      });
    });

    await page.goto("/invoices");
    // Header reflects the TRUE total, not the loaded page size.
    await expect(page.getByText(/showing 1 of 2 invoices/i)).toBeVisible();
    await expect(page.getByText("INV-2026-05").last()).toBeVisible();
    await expect(page.getByText("Page 1 of 2", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: /next page/i }).click();

    await expect(page.getByText("INV-2026-04").last()).toBeVisible();
    await expect(page.getByText("Page 2 of 2", { exact: true })).toBeVisible();
  });
});
