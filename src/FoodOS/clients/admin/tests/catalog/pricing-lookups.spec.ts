import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Catalog.PriceLists.View", "Permissions.Catalog.PriceLists.Create", "Permissions.Catalog.PriceLists.Update", "Permissions.Catalog.Products.View", "Permissions.Ordering.Customers.View"];
const customer = { id: "11111111-1111-1111-1111-111111111111", code: "ACME", name: "Acme" };
const list = { id: "33333333-3333-3333-3333-333333333333", name: "Test list", customerOrgId: null, priority: 0, validFrom: "2026-09-01T00:00:00Z", validTo: null, lines: [] };
for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} pricing lookups recover and all three product choices reach later pages`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route("**/api/v1/catalog/price-lists**", route => route.fulfill({ json: [list] }));
    let customers = 0, customerFailure = true;
    await page.route("**/api/v1/ordering/customer-orgs**", route => {
      customers++;
      expect(route.request().headers().tenant).toBe("root");
      return route.fulfill(customerFailure ? { status: 403, json: {} } : { json: [customer] });
    });
    let secondFailed = false;
    await page.route("**/api/v1/catalog/products?**", route => {
      expect(route.request().headers().tenant).toBe("root");
      const url = new URL(route.request().url());
      expect(url.searchParams.get("pageSize")).toBe("50");
      if (url.searchParams.get("search")) return route.fulfill({ json: paged([]) });
      const pageNumber = Number(url.searchParams.get("pageNumber"));
      if (pageNumber === 2 && !secondFailed) { secondFailed = true; return route.fulfill({ status: 403, json: {} }); }
      return route.fulfill({ json: paged([{ id: `product-${pageNumber}`, name: `Product ${pageNumber}`, sku: `SKU-${pageNumber}` }], { pageNumber, pageSize: 50, totalCount: 51, totalPages: 2 }) });
    });
    const writes: Record<string, unknown>[] = [];
    for (const url of [`**/api/v1/catalog/price-lists/${list.id}/lines`, "**/api/v1/catalog/price-locks"]) await page.route(url, route => {
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      writes.push(route.request().postDataJSON());
      return route.fulfill({ json: "saved" });
    });
    await page.route("**/api/v1/catalog/quotes**", route => {
      const url = new URL(route.request().url());
      expect(url.searchParams.get("productId")).toBe("product-2");
      expect(route.request().headers().tenant).toBe("root");
      return route.fulfill({ json: { unitPrice: 2, currency: "USD", source: "Catalog" } });
    });
    await page.goto("/catalog/pricing");
    await expect(page.getByRole("button", { name: messages.pricing.newList, exact: true })).toBeDisabled();
    await expect(page.getByRole("button", { name: messages.pricing.setLock, exact: true })).toBeDisabled();
    const failedCustomerReads = customers;
    customerFailure = false;
    await page.getByRole("region", { name: messages.pricing.customersFailed, exact: true }).getByRole("button", { name: messages.workbench.retry }).click();
    await expect(page.getByRole("button", { name: messages.pricing.newList, exact: true })).toBeEnabled();
    for (const mode of ["tier", "lock", "quote"]) {
      if (mode !== "quote") await page.getByRole("button", { name: mode === "tier" ? messages.pricing.addOrReplaceTier : messages.pricing.setLock, exact: true }).click();
      const container = mode === "quote" ? page.getByRole("heading", { name: messages.pricing.quoteTitle, exact: true }).locator("..").locator("..") : page.getByRole("dialog");
      const choice = container.getByRole("region", { name: messages.pricing.product, exact: true });
      await choice.getByRole("button", { name: messages.common.next, exact: true }).click();
      if (mode === "tier") {
        await expect(choice.getByRole("alert")).toHaveText(messages.pricing.productsFailed);
        await choice.getByRole("button", { name: messages.workbench.retry, exact: true }).click();
      }
      await choice.getByRole("combobox").selectOption("product-2");
      await choice.getByRole("searchbox").fill("missing");
      await expect(choice.getByRole("status")).toHaveText(messages.common.emptyDefault);
      await expect(choice.getByRole("combobox")).toHaveValue("product-2");
      if (mode !== "tier") await container.getByRole("combobox", { name: messages.pricing.customer, exact: true }).selectOption(customer.id);
      if (mode !== "quote") await container.getByRole("spinbutton", { name: messages.pricing.unitPriceUsd, exact: true }).fill("2");
      if (mode === "lock") await container.getByLabel(messages.pricing.lockUntil).fill("2027-01-01T12:00");
      await container.getByRole("button", { name: mode === "tier" ? messages.pricing.saveTier : mode === "lock" ? messages.pricing.saveLock : messages.pricing.quote, exact: true }).click();
      if (mode !== "quote") await expect(container).toHaveCount(0);
      else await expect(container.getByRole("status").filter({ hasText: "Catalog" })).toBeVisible();
    }
    expect(writes).toHaveLength(2);
    expect(writes.every(body => body.productId === "product-2" && body.currency === "USD")).toBe(true);
    expect(customers).toBe(failedCustomerReads + 1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
