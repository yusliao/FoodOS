import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const list = { id: "33333333-3333-3333-3333-333333333333", name: "Contract", priority: 0, validFrom: "2026-09-01T00:00:00Z", validTo: null, lines: [] };
async function setup(page: Page, culture: string) {
  const permissions = ["Permissions.Catalog.PriceLists.View", "Permissions.Catalog.PriceLists.Update", "Permissions.Catalog.Products.View", "Permissions.Ordering.Customers.View"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route("**/api/v1/catalog/price-lists**", route => route.fulfill({ json: [list] }));
  await page.route("**/api/v1/catalog/products?**", route => route.fulfill({ json: paged([{ id: "product-1", name: "Spinach" }, { id: "product-2", name: "Kale" }]) }));
  await page.route("**/api/v1/ordering/customer-orgs**", route => route.fulfill({ json: [{ id: "customer-1", name: "Acme", code: "A" }, { id: "customer-2", name: "Beta", code: "B" }] }));
}
for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const mode of ["tier", "lock"]) test(`${culture} ${mode} freezes inputs and retains idempotency only for identical retry`, async ({ page }) => {
    await setup(page, culture);
    let release!: () => void;
    const held = new Promise<void>(resolve => { release = resolve; });
    const writes: { key: string; body: Record<string, unknown> }[] = [];
    await page.route(mode === "tier" ? `**/api/v1/catalog/price-lists/${list.id}/lines` : "**/api/v1/catalog/price-locks", async route => {
      expect(route.request().headers().tenant).toBe("root");
      writes.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
      if (writes.length === 1) await held;
      await route.fulfill(writes.length < 3 ? { status: 403, json: { detail: "Pricing write denied" } } : { json: "saved" });
    });
    await page.goto("/catalog/pricing");
    await page.getByRole("button", { name: mode === "tier" ? m.pricing.addOrReplaceTier : m.pricing.setLock, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox", { name: m.pricing.product, exact: true }).selectOption("product-1");
    if (mode === "lock") {
      await dialog.getByRole("combobox", { name: m.pricing.customer, exact: true }).selectOption("customer-1");
      await dialog.getByLabel(m.pricing.lockUntil).fill("2027-01-01T12:00");
    }
    const price = dialog.getByRole("spinbutton", { name: m.pricing.unitPriceUsd, exact: true });
    const save = dialog.getByRole("button", { name: mode === "tier" ? m.pricing.saveTier : m.pricing.saveLock, exact: true });
    await expect(save).toBeDisabled();
    await price.fill("2");
    await save.click();
    await expect.poll(() => writes.length).toBe(1);
    for (const field of await dialog.locator("input, select").all()) await expect(field).toBeDisabled();
    await expect(dialog.getByRole("button", { name: m.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    release();
    await expect(dialog.getByRole("alert")).toHaveText("Pricing write denied");
    await expect(price).toHaveValue("2");
    await save.click();
    await expect.poll(() => writes.length).toBe(2);
    await expect(save).toBeEnabled();
    expect(writes[0].key).toBeTruthy();
    expect(writes[1]).toEqual(writes[0]);
    await price.fill("0");
    await save.click();
    await expect(dialog).toHaveCount(0);
    expect(writes).toHaveLength(3);
    expect(writes[2].key).not.toBe(writes[0].key);
    expect(writes[2].body).toMatchObject({ productId: "product-1", unitPrice: 0, currency: "USD" });
  });

  test(`${culture} quote result is cleared by every condition and failure`, async ({ page }) => {
    await setup(page, culture);
    let calls = 0, fail = false;
    let release!: () => void;
    const held = new Promise<void>(resolve => { release = resolve; });
    await page.route("**/api/v1/catalog/quotes**", async route => {
      expect(route.request().headers().tenant).toBe("root");
      calls++;
      if (calls === 1) await held;
      await route.fulfill(fail ? { status: 403, json: { detail: "Quote denied" } } : { json: { unitPrice: 2, currency: "USD", source: "Catalog" } });
    });
    await page.goto("/catalog/pricing");
    const panel = page.getByRole("heading", { name: m.pricing.quoteTitle, exact: true }).locator("..").locator("..");
    const customer = panel.getByRole("combobox", { name: m.pricing.customer, exact: true });
    const product = panel.getByRole("combobox", { name: m.pricing.product, exact: true });
    const quantity = panel.getByRole("spinbutton", { name: m.pricing.quantity, exact: true });
    const result = panel.getByRole("status").filter({ hasText: "Catalog" });
    const submit = panel.getByRole("button", { name: m.pricing.quote, exact: true });
    await customer.selectOption("customer-1");
    await product.selectOption("product-1");
    await submit.click();
    await expect.poll(() => calls).toBe(1);
    for (const field of [customer, product, quantity]) await expect(field).toBeDisabled();
    release();
    await expect(result).toBeVisible();
    for (const change of [() => quantity.fill("2"), () => customer.selectOption("customer-2"), () => product.selectOption("product-2")]) {
      await change();
      await expect(result).toHaveCount(0);
      await submit.click();
      await expect(result).toBeVisible();
    }
    fail = true;
    await submit.click();
    await expect(panel.getByText("Quote denied")).toBeVisible();
    await expect(result).toHaveCount(0);
    fail = false;
    await submit.click();
    await expect(result).toBeVisible();
    expect(calls).toBe(6);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
