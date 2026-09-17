import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Catalog.Products.View", "Permissions.Catalog.Products.Create", "Permissions.Catalog.Products.Update", "Permissions.Catalog.Brands.View", "Permissions.Catalog.Categories.View"];
const product = { id: "33333333-3333-3333-3333-333333333333", sku: "VEG-1", name: "Spinach", brandId: "b", categoryId: "c", isActive: true, price: { amount: 3.5, currency: "USD" } };

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const mode of ["create", "edit"] as const) {
    test(`${culture} ${mode} retries only failed lookup then opens the requested form`, async ({ page }) => {
      await seedAuthedSession(page, { ...TEST_USER, permissions });
      await installAdminShellMocks(page, permissions);
      await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
      await page.setViewportSize({ width: 390, height: 844 });
      await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
      let failed = true;
      const failureKind = mode === "create" ? "brands" : "categories";
      const healthyKind = failureKind === "brands" ? "categories" : "brands";
      const calls = { brands: 0, categories: 0 };
      for (const kind of ["brands", "categories"] as const) {
        await page.route(`**/api/v1/catalog/${kind}?**`, route => {
          calls[kind]++;
          expect(route.request().headers().tenant).toBe("root");
          return route.fulfill(kind === failureKind && failed ? { status: 403, json: {} } : { json: paged([{ id: kind === "brands" ? "b" : "c", name: kind }]) });
        });
      }
      await page.goto("/catalog/products");
      await expect(page.getByRole("heading", { name: product.name, exact: true })).toBeVisible();
      expect(calls).toEqual({ brands: 0, categories: 0 });
      await page.getByRole("button", { name: mode === "create" ? messages.catalog.products.new : messages.catalog.edit, exact: true }).click();
      const preparation = page.getByRole("region", { name: messages.catalog.products.loadingForm });
      await expect(preparation).toContainText(messages.catalog.products.lookupFailed);
      await expect(page.getByRole("dialog")).toHaveCount(0);
      const healthyCalls = calls[healthyKind];
      failed = false;
      await preparation.getByRole("button", { name: messages.workbench.retry, exact: true }).click();
      const dialog = page.getByRole("dialog");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("textbox", { name: messages.catalog.name, exact: true })).toHaveValue(mode === "edit" ? product.name : "");
      expect(calls[failureKind]).toBe(2);
      expect(calls[healthyKind]).toBe(healthyCalls);
      await dialog.getByRole("button", { name: messages.chrome.cancel, exact: true }).click();
      await expect(dialog).toHaveCount(0);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    });
  }
}

test("canceling pending lookups aborts preparation and does not open a late form", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
  let hold = true;
  const releases: (() => void)[] = [];
  const aborted: string[] = [];
  page.on("requestfailed", request => { if (/\/catalog\/(brands|categories)\?/.test(request.url())) aborted.push(request.url()); });
  for (const kind of ["brands", "categories"]) await page.route(`**/api/v1/catalog/${kind}?**`, async route => {
    if (hold) await new Promise<void>(resolve => releases.push(resolve));
    await route.fulfill({ json: paged([{ id: kind === "brands" ? "b" : "c", name: kind }]) });
  });
  await page.goto("/catalog/products");
  await page.getByRole("button", { name: en.catalog.products.new, exact: true }).click();
  await expect.poll(() => releases.length).toBe(2);
  const preparation = page.getByRole("region", { name: en.catalog.products.loadingForm });
  await preparation.getByRole("button", { name: en.chrome.cancel, exact: true }).click();
  await expect.poll(() => aborted.length).toBe(2);
  hold = false;
  releases.forEach(release => release());
  await expect(preparation).toHaveCount(0);
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.getByRole("button", { name: en.catalog.products.new, exact: true }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
});
