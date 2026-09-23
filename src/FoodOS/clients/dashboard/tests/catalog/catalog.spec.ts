import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({
      json: [
        "Permissions.Catalog.Products.View",
        "Permissions.Catalog.Products.Create",
        "Permissions.Catalog.Brands.View",
        "Permissions.Catalog.Categories.View",
      ],
    }),
  );
});

for (const path of [
  "/catalog",
  "/catalog/products",
  "/catalog/products/product-1",
  "/catalog/brands",
  "/catalog/categories",
]) {
  test(`operator catalog route is retired: ${path}`, async ({ page }) => {
    const requests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/(catalog|inventory)/.test(request.url())) requests.push(request.url());
    });

    await page.goto(path);

    await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
    await expect(page.getByText(`Retired route: ${path}`)).toBeVisible();
    expect(requests).toEqual([]);
  });
}

test("operator catalog is absent from navigation and command palette", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("button", { name: "Catalog", exact: true })).toHaveCount(0);
  await page.keyboard.press("Control+k");
  await expect(page.getByRole("combobox", { name: "Search commands" })).toBeVisible();
  for (const name of ["Products", "Brands", "Categories", "Create product", "Create brand", "Create category"]) {
    await expect(page.getByRole("option", { name: new RegExp(name, "i") })).toHaveCount(0);
  }
});
