import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Catalog.Products.View", "Permissions.Catalog.Products.Create", "Permissions.Catalog.Products.Update", "Permissions.Catalog.Brands.View", "Permissions.Catalog.Categories.View"];
const product = { id: "33333333-3333-3333-3333-333333333333", sku: "VEG-1", name: "Spinach", brandId: "old-brand", categoryId: "old-category", isActive: true, price: { amount: 3.5, currency: "USD" } };

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} product choices reach later pages, retry and preserve selections across search`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
    let brandFailed = false;
    const requests: string[] = [];
    for (const kind of ["brands", "categories"]) await page.route(`**/api/v1/catalog/${kind}?**`, route => {
      const url = new URL(route.request().url());
      requests.push(url.search);
      expect(url.searchParams.get("pageSize")).toBe("50");
      expect(route.request().headers().tenant).toBe("root");
      const pageNumber = Number(url.searchParams.get("pageNumber"));
      if (url.searchParams.has("search")) {
        expect(pageNumber).toBe(1);
        return route.fulfill({ json: paged([]) });
      }
      if (kind === "brands" && pageNumber === 2 && !brandFailed) {
        brandFailed = true;
        return route.fulfill({ status: 403, json: {} });
      }
      return route.fulfill({ json: paged([{ id: `${kind}-${pageNumber}`, name: `${kind} page ${pageNumber}` }], { pageNumber, pageSize: 50, totalPages: 2, totalCount: 51 }) });
    });
    let body: unknown;
    await page.route(`**/api/v1/catalog/products/${product.id}`, async route => { body = route.request().postDataJSON(); await route.fulfill({ json: product.id }); });
    await page.goto("/catalog/products");
    await page.getByRole("button", { name: messages.catalog.edit, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    // Existing associations outside the loaded page remain explicit, not blank.
    await expect(dialog.getByRole("combobox", { name: messages.catalog.products.brand, exact: true })).toHaveValue(product.brandId);
    await expect(dialog.getByRole("combobox", { name: messages.catalog.products.category, exact: true })).toHaveValue(product.categoryId);
    await expect(dialog.locator("option:checked").filter({ hasText: /^old-/ })).toHaveCount(2);
    for (const [kind, label] of [["brands", messages.catalog.products.brand], ["categories", messages.catalog.products.category]]) {
      const group = dialog.getByRole("region", { name: label, exact: true });
      await group.getByRole("button", { name: messages.common.next, exact: true }).click();
      if (kind === "brands") {
        await expect(group.getByRole("alert")).toBeVisible();
        await expect(dialog.getByRole("textbox", { name: messages.catalog.name, exact: true })).toHaveValue(product.name);
        await group.getByRole("button", { name: messages.workbench.retry, exact: true }).click();
      }
      await expect(group.getByRole("option", { name: `${kind} page 2`, exact: true })).toBeAttached();
      await group.getByRole("combobox").selectOption(`${kind}-2`);
      await group.getByRole("button", { name: messages.common.previous, exact: true }).click();
      await expect(group.getByRole("combobox")).toHaveValue(`${kind}-2`);
      await group.getByRole("searchbox").fill("no-match");
      await expect(group.getByRole("status")).toHaveText(messages.common.emptyDefault);
      await expect(group.getByRole("combobox")).toHaveValue(`${kind}-2`);
    }
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(body).toMatchObject({ brandId: "brands-2", categoryId: "categories-2", name: product.name });
    expect(requests.filter(query => query.includes("pageNumber=2"))).toHaveLength(3);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
