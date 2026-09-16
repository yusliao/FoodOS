import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

const BV = "Permissions.Catalog.Brands.View";
const BC = "Permissions.Catalog.Brands.Create";
const CV = "Permissions.Catalog.Categories.View";
const CU = "Permissions.Catalog.Categories.Update";
const PV = "Permissions.Catalog.Products.View";
const PC = "Permissions.Catalog.Products.Create";
const PU = "Permissions.Catalog.Products.Update";
const brand = { id: "11111111-1111-1111-1111-111111111111", name: "Fresh Fields", slug: "fresh-fields", description: "Produce", logoUrl: null, createdAtUtc: "2026-09-16T00:00:00Z" };
const category = { id: "22222222-2222-2222-2222-222222222222", name: "Vegetables", slug: "vegetables", description: "Greens", parentCategoryId: null, createdAtUtc: "2026-09-16T00:00:00Z" };
const product = { id: "33333333-3333-3333-3333-333333333333", sku: "VEG-01", name: "Baby spinach", slug: "baby-spinach", description: "Washed", brandId: brand.id, categoryId: category.id, price: { amount: 3.5, currency: "USD" }, isActive: true, createdAtUtc: "2026-09-16T00:00:00Z" };

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await mockJsonResponse(page, "**/api/v1/catalog/brands**", paged([brand]), { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/catalog/categories**", paged([category], { pageSize: 50 }), { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/catalog/products**", paged([product]), { method: "GET" });
}

test("product reader sees base prices without write actions or forbidden lookup requests", async ({ page }) => {
  await setup(page, [PV]);
  const requests: string[] = [];
  page.on("request", request => requests.push(request.url()));
  const productRequest = page.waitForRequest(request => request.url().includes("/catalog/products?"));
  await page.goto("/catalog/products");
  await expect(page.getByRole("heading", { name: "Baby spinach" })).toBeVisible();
  await expect(page.getByText("$3.50")).toBeVisible();
  await expect(page.getByRole("button", { name: /New product|Edit|Change base price|Delete/ })).toHaveCount(0);
  expect(requests.some(url => /catalog\/(brands|categories)/.test(url))).toBe(false);
  const request = await productRequest;
  expect(request.headers().tenant).toBe("root");
});

for (const [path, permission] of [["products", PV], ["brands", BV], ["categories", CV]] as const) {
  test(`direct ${path} URL is denied before its catalog request`, async ({ page }) => {
    await setup(page, []);
    const requests: string[] = [];
    page.on("request", request => requests.push(request.url()));
    await page.goto(`/catalog/${path}`);
    await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
    await expect(page.getByRole("main")).toContainText(permission);
    expect(requests.some(url => url.includes(`/api/v1/catalog/${path}`))).toBe(false);
  });
}

test("a 403 is distinct from empty state and the brand list can retry", async ({ page }) => {
  await setup(page, [BV]);
  let fails = true;
  await page.route("**/api/v1/catalog/brands**", async route => route.fulfill(fails ? { status: 403, json: { detail: "Brand access revoked" } } : { json: paged([]) }));
  await page.goto("/catalog/brands");
  await expect(page.getByText("Brand access revoked")).toBeVisible();
  await expect(page.getByText("No brands match this search.")).toHaveCount(0);
  fails = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No brands match this search.")).toBeVisible();
});

test("brand creation uses the root operator identity and refreshes the list", async ({ page }) => {
  await setup(page, [BV, BC]);
  let posted: Record<string, unknown> | undefined;
  await page.route("**/api/v1/catalog/brands", async route => {
    expect(route.request().headers().tenant).toBe("root");
    posted = route.request().postDataJSON();
    await route.fulfill({ json: brand.id });
  });
  await page.goto("/catalog/brands");
  await page.getByRole("button", { name: "New brand" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill("Harvest Co");
  await dialog.getByLabel("Description").fill("Local farms");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog).toHaveCount(0);
  expect(posted).toEqual({ name: "Harvest Co", description: "Local farms", logoUrl: "" });
});

test("category update keeps the server-defined parent relationship payload", async ({ page }) => {
  await setup(page, [CV, CU]);
  let posted: Record<string, unknown> | undefined;
  await page.route(`**/api/v1/catalog/categories/${category.id}`, async route => { posted = route.request().postDataJSON(); await route.fulfill({ json: category.id }); });
  await page.goto("/catalog/categories");
  await page.getByRole("button", { name: "Edit" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name").fill("Leafy vegetables");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog).toHaveCount(0);
  expect(posted).toMatchObject({ categoryId: category.id, name: "Leafy vegetables", parentCategoryId: null });
});

test("product creation and base-price update use only existing catalog endpoints", async ({ page }) => {
  await setup(page, [PV, PC, PU, BV, CV]);
  const writes: { url: string; body: Record<string, unknown>; tenant: string }[] = [];
  await page.route("**/api/v1/catalog/products", async route => { writes.push({ url: route.request().url(), body: route.request().postDataJSON(), tenant: route.request().headers().tenant }); await route.fulfill({ json: product.id }); });
  await page.route(`**/api/v1/catalog/products/${product.id}/price`, async route => { writes.push({ url: route.request().url(), body: route.request().postDataJSON(), tenant: route.request().headers().tenant }); await route.fulfill({ json: product.id }); });
  await page.goto("/catalog/products");
  await page.getByRole("button", { name: "New product" }).click();
  const create = page.getByRole("dialog");
  await create.getByLabel("SKU").fill("VEG-02");
  await create.getByLabel("Name").fill("Kale");
  await create.getByLabel("Brand ID").selectOption(brand.id);
  await create.getByLabel("Category ID").selectOption(category.id);
  await create.getByLabel("Base price (USD)").fill("4.25");
  await create.getByRole("button", { name: "Save" }).click();
  await expect(create).toHaveCount(0);
  await page.getByRole("button", { name: "Change base price" }).click();
  const price = page.getByRole("dialog");
  await price.getByLabel("New base price").fill("3.75");
  await price.getByRole("button", { name: "Save" }).click();
  await expect(price).toHaveCount(0);
  expect(writes[0]).toMatchObject({ tenant: "root", body: { sku: "VEG-02", name: "Kale", brandId: brand.id, categoryId: category.id, priceAmount: 4.25, priceCurrency: "USD", stock: 0 } });
  expect(writes[1]).toMatchObject({ tenant: "root", body: { productId: product.id, amount: 3.75, currency: "USD" } });
  expect(writes.every(write => !/price-lists|price-locks|quotes/.test(write.url))).toBe(true);
});

test("catalog cards remain usable in the mobile viewport", async ({ page }, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setup(page, [BV]);
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/catalog/brands");
  await expect(page.getByRole("heading", { name: "品牌", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Fresh Fields" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath("catalog-mobile.png"), fullPage: true });
});
