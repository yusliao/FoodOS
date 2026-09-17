import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const item = { id: "33333333-3333-3333-3333-333333333333", name: "Catalog item", slug: "catalog-item", sku: "ITEM-1", brandId: "brand", categoryId: "category", price: { amount: 3.5, currency: "USD" }, isActive: true };
for (const kind of ["products", "brands", "categories"] as const) {
  const resource = kind[0].toUpperCase() + kind.slice(1);
  for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) test(`${culture} ${kind} delete error stays in dialog, cancels cleanly and retries safely`, async ({ page }) => {
    const permissions = [`Permissions.Catalog.${resource}.View`, `Permissions.Catalog.${resource}.Delete`];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let deleted = false;
    let releaseRefresh!: () => void;
    const refresh = new Promise<void>(resolve => { releaseRefresh = resolve; });
    let refreshing = false;
    await page.route(`**/api/v1/catalog/${kind}?**`, async route => {
      if (deleted) { refreshing = true; await refresh; }
      await route.fulfill({ json: paged(deleted ? [] : [item]) });
    });
    let releaseDelete!: () => void;
    const pending = new Promise<void>(resolve => { releaseDelete = resolve; });
    let deletes = 0;
    const failure = kind === "categories" ? "Cannot delete a category that has child categories." : "Delete permission denied";
    await page.route(`**/api/v1/catalog/${kind}/${item.id}`, async route => {
      expect(route.request().method()).toBe("DELETE");
      expect(route.request().headers().tenant).toBe("root");
      deletes++;
      if (deletes === 1) return route.fulfill({ status: kind === "categories" ? 409 : 403, json: { detail: failure } });
      await pending;
      deleted = true;
      await route.fulfill({ status: 204 });
    });
    await page.goto(`/catalog/${kind}`);
    await page.getByRole("button", { name: messages.catalog.delete, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: messages.catalog.delete, exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText(failure);
    await dialog.getByRole("button", { name: messages.chrome.cancel, exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole("alert")).toHaveCount(0);
    await page.getByRole("button", { name: messages.catalog.delete, exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveCount(0);
    await dialog.getByRole("button", { name: messages.catalog.delete, exact: true }).click();
    await expect.poll(() => deletes).toBe(2);
    await expect(dialog.getByRole("button", { name: messages.common.working, exact: true })).toBeDisabled();
    await expect(dialog.getByRole("button", { name: messages.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    releaseDelete();
    await expect.poll(() => refreshing).toBe(true);
    await expect(dialog.getByRole("button", { name: messages.common.working, exact: true })).toBeDisabled();
    releaseRefresh();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole("heading", { name: item.name })).toHaveCount(0);
    expect(deletes).toBe(2);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });

  test(`${kind} update permission does not grant delete`, async ({ page }) => {
    const permissions = [`Permissions.Catalog.${resource}.View`, `Permissions.Catalog.${resource}.Update`];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    const writes: string[] = [];
    await page.route(`**/api/v1/catalog/${kind}**`, route => {
      if (route.request().method() !== "GET") writes.push(route.request().method());
      return route.fulfill({ json: paged([item]) });
    });
    await page.goto(`/catalog/${kind}`);
    await expect(page.getByRole("heading", { name: item.name })).toBeVisible();
    await expect(page.getByRole("button", { name: "Delete", exact: true })).toHaveCount(0);
    expect(writes).toEqual([]);
  });
}
