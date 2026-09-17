import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const product = { id: "33333333-3333-3333-3333-333333333333", name: "Spinach", sku: "VEG-1", brandId: "b", categoryId: "c", isActive: true, price: { amount: 3.5, currency: "USD" } };
const permissions = ["Permissions.Catalog.Products.View", "Permissions.Catalog.Products.Update"];

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} base price freezes through write and refresh, preserves failure and accepts explicit zero`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let releaseWrite: (() => void) | undefined;
    let releaseRefresh: (() => void) | undefined;
    let saved = false;
    const writes: unknown[] = [];
    await page.route("**/api/v1/catalog/products?**", async route => {
      if (saved) await new Promise<void>(resolve => { releaseRefresh = resolve; });
      await route.fulfill({ json: paged([{ ...product, price: { ...product.price, amount: saved ? 0 : 3.5 } }]) });
    });
    await page.route(`**/api/v1/catalog/products/${product.id}/price`, async route => {
      expect(route.request().method()).toBe("PATCH");
      expect(route.request().headers().tenant).toBe("root");
      writes.push(route.request().postDataJSON());
      if (writes.length === 1) {
        await new Promise<void>(resolve => { releaseWrite = resolve; });
        await route.fulfill({ status: 403, json: { detail: "Price change denied" } });
      } else {
        saved = true;
        await route.fulfill({ json: product.id });
      }
    });
    await page.goto("/catalog/products");
    await page.getByRole("button", { name: messages.catalog.products.changePrice, exact: true }).click();
    const dialog = page.getByRole("dialog");
    const input = dialog.getByRole("spinbutton", { name: messages.catalog.products.newPrice, exact: true });
    const save = dialog.getByRole("button", { name: messages.catalog.save, exact: true });
    await input.fill("");
    await expect(save).toBeDisabled();
    await input.fill("-1");
    await expect(save).toBeDisabled();
    expect(writes).toEqual([]);
    await input.fill("4.25");
    await save.click();
    await expect.poll(() => !!releaseWrite).toBe(true);
    await expect(input).toBeDisabled();
    await expect(dialog.getByRole("button", { name: messages.catalog.saving, exact: true })).toBeDisabled();
    await expect(dialog.getByRole("button", { name: messages.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    releaseWrite?.();
    await expect(dialog.getByRole("alert")).toHaveText("Price change denied");
    await expect(input).toBeEnabled();
    await expect(input).toHaveValue("4.25");
    await input.fill("0");
    await save.click();
    await expect.poll(() => !!releaseRefresh).toBe(true);
    await expect(input).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    releaseRefresh?.();
    await expect(dialog).toHaveCount(0);
    expect(writes).toEqual([{ productId: product.id, amount: 4.25, currency: "USD" }, { productId: product.id, amount: 0, currency: "USD" }]);
    await page.getByRole("button", { name: messages.catalog.products.changePrice, exact: true }).click();
    await expect(input).toHaveValue("0");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
