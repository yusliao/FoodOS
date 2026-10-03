import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const product = { id: "product-1", sku: "SKU-1", name: "Spinach", baseUom: "EA", isActive: true, price: { amount: 3, currency: "USD" } };
for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} inventory shows unknown, stale and current quantities and recovers errors`, async ({ page }, info) => {
    const permissions = ["Permissions.Catalog.Products.View", "Permissions.WmsIntegration.View"];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
    let status = "notConfigured", calls = 0;
    await page.route("**/api/v1/wms/availability?**", route => {
      calls++;
      const url = new URL(route.request().url());
      expect(url.searchParams.get("sku")).toBe(product.sku);
      expect(url.searchParams.get("uom")).toBe(product.baseUom);
      expect(route.request().headers().tenant).toBe("root");
      return route.fulfill(status === "error" ? { status: 503, json: { detail: "Inventory service unavailable" } } : { json: {
        warehouseCode: "DC-01", inventory: { sku: product.sku, uom: "EA", availableQuantity: status === "insufficient" ? 0 : 12,
          isAvailable: status === "available", asOf: ["stale", "available", "insufficient"].includes(status) ? new Date().toISOString() : null, status },
      } });
    });
    await page.goto("/catalog/products");
    await page.getByRole("button", { name: messages.catalog.inventory.view }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("status")).toHaveText(messages.catalog.inventory.notConfigured);
    await expect(dialog.getByText("12 EA", { exact: true })).toHaveCount(0);
    for (const state of ["notSynced", "stale", "available", "insufficient"] as const) {
      status = state;
      await dialog.getByRole("button", { name: messages.catalog.inventory.refresh }).click();
      await expect(dialog.getByRole("status")).toHaveText(messages.catalog.inventory[state]);
      if (state !== "notSynced") await expect(dialog.getByText(state === "insufficient" ? "0 EA" : "12 EA", { exact: true })).toBeVisible();
    }
    await page.screenshot({ path: info.outputPath("inventory-mobile.png"), fullPage: true });
    status = "error";
    await dialog.getByRole("button", { name: messages.catalog.inventory.refresh }).click();
    await expect(dialog.getByRole("alert")).toHaveText("Inventory service unavailable");
    status = "available";
    await dialog.getByRole("button", { name: messages.catalog.inventory.refresh }).click();
    await expect(dialog.getByRole("status")).toHaveText(messages.catalog.inventory.available);
    expect(calls).toBeGreaterThanOrEqual(7);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("product reader without WMS permission does not request inventory", async ({ page }) => {
  const permissions = ["Permissions.Catalog.Products.View"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  let calls = 0;
  await page.route("**/api/v1/wms/availability?**", route => { calls++; return route.fulfill({ status: 403 }); });
  await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([product]) }));
  await page.goto("/catalog/products");
  await expect(page.getByRole("heading", { name: product.name, exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: en.catalog.inventory.view })).toHaveCount(0);
  expect(calls).toBe(0);
});
