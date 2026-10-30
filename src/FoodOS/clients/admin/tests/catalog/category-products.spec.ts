import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Catalog.Products.View", "Permissions.Catalog.Categories.View"];
const poultry = { id: "11111111-1111-1111-1111-111111111111", name: "禽肉", slug: "qin-rou", description: "", parentCategoryId: null };
const seafood = { id: "22222222-2222-2222-2222-222222222222", name: "海鲜水产", slug: "hai-xian", description: "", parentCategoryId: null };
const chicken = { id: "33333333-3333-3333-3333-333333333333", sku: "1141", name: "無骨胸 Boneless Breast", brandId: "b", brandName: "未指定品牌", categoryId: poultry.id, categoryName: poultry.name, baseUom: "CS", isActive: false, price: { amount: 0, currency: "USD" } };

test("分类页查看分类下商品，并跳到带分类筛选的商品列表", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.route("**/api/v1/catalog/categories?**", route => route.fulfill({ json: paged([poultry, seafood]) }));
  const productQueries: (string | null)[] = [];
  await page.route("**/api/v1/catalog/products?**", route => {
    const categoryId = new URL(route.request().url()).searchParams.get("categoryId");
    productQueries.push(categoryId);
    return route.fulfill({ json: paged(categoryId === seafood.id ? [] : [chicken]) });
  });

  await page.goto("/catalog/categories");
  await page.getByRole("article").filter({ hasText: poultry.name }).getByRole("button", { name: zh.catalog.categories.viewProducts }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("cell", { name: chicken.sku, exact: true })).toBeVisible();
  await expect(dialog.getByRole("cell", { name: zh.catalog.products.hidden, exact: true })).toBeVisible();
  expect(productQueries).toContain(poultry.id);

  await dialog.getByRole("link", { name: zh.catalog.categories.openInProducts }).click();
  await expect(page).toHaveURL(new RegExp(`/catalog/products\\?categoryId=${poultry.id}`));
  const filter = page.getByRole("combobox", { name: zh.catalog.products.categoryFilter, exact: true });
  await expect(filter).toHaveValue(poultry.id);
  await expect(page.getByRole("heading", { name: chicken.name, exact: true })).toBeVisible();

  await filter.selectOption(seafood.id);
  await expect(page.getByText(zh.catalog.products.empty)).toBeVisible();
  expect(productQueries).toContain(seafood.id);
  await filter.selectOption("");
  await expect(page).not.toHaveURL(/categoryId=/);
  await expect(page.getByRole("heading", { name: chicken.name, exact: true })).toBeVisible();
});
