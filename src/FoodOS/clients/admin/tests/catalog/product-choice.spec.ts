import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Catalog.Products.View", "Permissions.Catalog.Products.Create", "Permissions.Catalog.Products.Update", "Permissions.Catalog.Brands.View", "Permissions.Catalog.Categories.View"];
const product = { id: "33333333-3333-3333-3333-333333333333", sku: "VEG-1", name: "Spinach", brandId: "old-brand", categoryId: "old-category", isActive: true, price: { amount: 3.5, currency: "USD" } };

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} lookup search Enter does not save and explicit save keeps existing associations`, async ({ page }, info) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
    for (const kind of ["brands", "categories"] as const) {
      await page.route(`**/api/v1/catalog/${kind}?**`, route => route.fulfill({ json: paged(
        new URL(route.request().url()).searchParams.has("search") ? [] : [{
          id: kind === "brands" ? product.brandId : product.categoryId, name: kind,
        }],
      ) }));
    }
    const writes: unknown[] = [];
    await page.route(`**/api/v1/catalog/products/${product.id}`, async route => {
      writes.push(route.request().postDataJSON());
      await route.fulfill({ status: 409, json: { detail: "Product update rejected" } });
    });
    await page.goto("/catalog/products");
    await page.getByRole("button", { name: messages.catalog.edit, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole("searchbox")).toHaveCount(0);
    for (const [label, searchLabel] of [[messages.catalog.products.brandLabel, messages.catalog.products.searchBrand], [messages.catalog.products.categoryLabel, messages.catalog.products.searchCategory]]) {
      const trigger = dialog.getByRole("button", { name: label, exact: true });
      await trigger.click();
      const menu = page.getByRole("menu", { name: label, exact: true });
      const input = menu.getByRole("searchbox", { name: searchLabel, exact: true });
      await expect(input).toBeFocused();
      await expect(input).toHaveAttribute("placeholder", searchLabel);
      if (culture === "zh-CN") await page.screenshot({ path: info.outputPath(`${label}-dropdown.png`), fullPage: true });
      await input.press("ArrowDown");
      await expect(menu.getByRole("menuitemradio", { checked: true })).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(menu).toHaveCount(0);
      await trigger.click();
      await expect(input).toBeFocused();
      await input.fill("no-match");
      await expect(menu.getByRole("status")).toHaveText(messages.common.emptyDefault);
      await input.press("Enter");
      await expect(menu).toBeVisible();
      await input.press("Escape");
      await expect(menu).toHaveCount(0);
      await expect(trigger).toBeFocused();
    }
    await dialog.getByRole("textbox", { name: messages.catalog.name, exact: true }).focus();
    expect(writes).toEqual([]);
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText("Product update rejected");
    expect(writes).toEqual([expect.objectContaining({ brandId: product.brandId, categoryId: product.categoryId, name: product.name })]);
    await page.route(`**/api/v1/catalog/products/${product.id}`, route => route.fulfill({ json: product.id }));
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog).toHaveCount(0);
  });

  test(`${culture} product choices reach later pages, retry and preserve selections across search`, async ({ page }, info) => {
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
    await expect(dialog.getByRole("button", { name: messages.catalog.products.brandLabel, exact: true })).toHaveText(product.brandId);
    await expect(dialog.getByRole("button", { name: messages.catalog.products.categoryLabel, exact: true })).toHaveText(product.categoryId);
    for (const [kind, label] of [["brands", messages.catalog.products.brandLabel], ["categories", messages.catalog.products.categoryLabel]]) {
      const trigger = dialog.getByRole("button", { name: label, exact: true });
      await trigger.click();
      const group = page.getByRole("menu", { name: label, exact: true });
      await expect(group.getByRole("menuitemradio", { checked: true })).toHaveText(kind === "brands" ? product.brandId : product.categoryId);
      if (culture === "zh-CN") await page.screenshot({ path: info.outputPath(`${kind}-mobile.png`), fullPage: true });
      await group.getByRole("button", { name: messages.common.next, exact: true }).click();
      if (kind === "brands") {
        await expect(group.getByRole("alert")).toBeVisible();
        await group.getByRole("searchbox").press("Escape");
        await expect(dialog.getByRole("textbox", { name: messages.catalog.name, exact: true })).toHaveValue(product.name);
        await trigger.click();
        await group.getByRole("button", { name: messages.workbench.retry, exact: true }).click();
      }
      await group.getByRole("menuitemradio", { name: `${kind} page 2`, exact: true }).click();
      await expect(group).toHaveCount(0);
      await expect(trigger).toHaveText(`${kind} page 2`);
      await trigger.click();
      await group.getByRole("button", { name: messages.common.previous, exact: true }).click();
      await expect(group.getByRole("menuitemradio", { checked: true })).toHaveText(`${kind} page 2`);
      await group.getByRole("searchbox").fill("no-match");
      await expect(group.getByRole("status")).toHaveText(messages.common.emptyDefault);
      await expect(group.getByRole("menuitemradio", { checked: true })).toHaveText(`${kind} page 2`);
      await group.getByRole("searchbox").press("Escape");
    }
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(body).toMatchObject({ brandId: "brands-2", categoryId: "categories-2", name: product.name });
    expect(requests.filter(query => query.includes("pageNumber=2"))).toHaveLength(3);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
