import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installShellMocks, paged } from "../helpers/shell-mocks";

const WAREHOUSE = {
  id: "33333333-3333-3333-3333-333333333333",
  code: "BOS-1",
  name: "Boston DC",
  city: "Boston",
  timeZoneId: "America/New_York",
  createdAtUtc: "2026-09-01T00:00:00Z",
  zones: [{ id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", code: "CH", kind: "Chilled" }],
};

const OPS_PERMS = [
  "Permissions.Procurement.Suppliers.Create",
  "Permissions.Procurement.Purchase.View",
  "Permissions.Procurement.Purchase.Create",
];

async function mockOpsApis(page: Page) {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", OPS_PERMS);
  await mockJsonResponse(page, "**/api/v1/inventory/warehouses**", paged([WAREHOUSE]));
  await mockJsonResponse(page, "**/api/v1/procurement/purchase-orders**", []);
}

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
});

test("purchasing desk creates a supplier", async ({ page }) => {
  await mockOpsApis(page);
  await mockJsonResponse(page, "**/api/v1/procurement/suppliers**", []);
  await mockJsonResponse(page, "**/api/v1/catalog/products**", paged([]));
  let posted: unknown;
  await page.route("**/api/v1/procurement/suppliers", async (route) => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    posted = route.request().postDataJSON();
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
    });
  });

  await page.goto("/ops/purchase");
  await expect(page.getByRole("heading", { name: /purchasing/i })).toBeVisible();
  await page.getByTestId("supplier-code").fill("FARM-1");
  await page.getByTestId("supplier-name").fill("North Farm");
  await page.getByTestId("supplier-create").click();
  await expect.poll(() => (posted as { code?: string } | undefined)?.code).toBe("FARM-1");
});

test("purchasing desk creates a draft PO", async ({ page }) => {
  await mockOpsApis(page);
  const supplierId = "16161616-1616-1616-1616-161616161616";
  const productId = "44444444-4444-4444-4444-444444444444";
  await mockJsonResponse(page, "**/api/v1/procurement/suppliers**", [
    { id: supplierId, code: "FARM-1", name: "North Farm", leadDays: 0, status: "Active", createdAtUtc: new Date().toISOString() },
  ]);
  await mockJsonResponse(
    page,
    "**/api/v1/catalog/products**",
    paged([
      {
        id: productId,
        sku: "COD-1",
        name: "Cod loin",
        slug: "cod-loin",
        brandId: "b",
        categoryId: "c",
        price: { amount: 9.5, currency: "USD" },
        stock: 0,
        isActive: true,
        temperatureZone: "Chilled",
        images: [],
        createdAtUtc: new Date().toISOString(),
      },
    ]),
  );
  let posted: unknown;
  await page.route("**/api/v1/procurement/purchase-orders", async (route) => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    posted = route.request().postDataJSON();
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
    });
  });

  await page.goto("/ops/purchase");
  await expect(page.getByRole("heading", { name: /purchasing/i })).toBeVisible();
  await page.getByTestId("po-qty").fill("12");
  await page.getByTestId("po-create").click();
  await expect.poll(() => (posted as { supplierId?: string; lines?: Array<{ quantity: number }> } | undefined)?.supplierId).toBe(supplierId);
  await expect.poll(() => (posted as { lines?: Array<{ quantity: number }> } | undefined)?.lines?.[0]?.quantity).toBe(12);
});
