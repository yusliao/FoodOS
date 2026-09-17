import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const brand = { id: "11111111-1111-1111-1111-111111111111", name: "Fresh Fields", slug: "fresh-fields", description: "Local farms", logoUrl: "https://example.com/logo.png" };
for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} brand updater preserves failures and freezes through save and refresh`, async ({ page }) => {
    const permissions = ["Permissions.Catalog.Brands.View", "Permissions.Catalog.Brands.Update"];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let saved = false, refreshing = false;
    let releaseRefresh!: () => void, releaseWrite!: () => void;
    const refresh = new Promise<void>(resolve => { releaseRefresh = resolve; });
    const write = new Promise<void>(resolve => { releaseWrite = resolve; });
    await page.route("**/api/v1/catalog/brands?**", async route => {
      if (saved) { refreshing = true; await refresh; }
      await route.fulfill({ json: paged([{ ...brand, name: saved ? "Harvest Co" : brand.name }]) });
    });
    const writes: Record<string, unknown>[] = [];
    await page.route(`**/api/v1/catalog/brands/${brand.id}`, async route => {
      expect(route.request().method()).toBe("PUT");
      expect(route.request().headers().tenant).toBe("root");
      writes.push(route.request().postDataJSON());
      if (writes.length === 1) return route.fulfill({ status: 403, json: { detail: "Brand update denied" } });
      if (writes.length === 2) return route.fulfill({ status: 409, json: { detail: "Another brand already exists" } });
      await write;
      saved = true;
      await route.fulfill({ json: brand.id });
    });
    await page.goto("/catalog/brands");
    await expect(page.getByRole("button", { name: messages.catalog.brands.new, exact: true })).toHaveCount(0);
    await page.getByRole("button", { name: messages.catalog.edit, exact: true }).click();
    const dialog = page.getByRole("dialog");
    const name = dialog.getByRole("textbox", { name: messages.catalog.name, exact: true });
    const description = dialog.getByRole("textbox", { name: messages.catalog.description, exact: true });
    const logo = dialog.getByRole("textbox", { name: messages.catalog.logoUrl, exact: true });
    await expect(logo).toHaveValue(brand.logoUrl);
    await name.fill("   ");
    await expect(dialog.getByRole("button", { name: messages.catalog.save, exact: true })).toBeDisabled();
    expect(writes).toHaveLength(0);
    await name.fill(" Harvest Co ");
    await description.fill(" Updated farms ");
    for (const error of ["Brand update denied", "Another brand already exists"]) {
      await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
      await expect(dialog.getByRole("alert")).toHaveText(error);
      await expect(name).toHaveValue(" Harvest Co ");
      await expect(description).toHaveValue(" Updated farms ");
    }
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect.poll(() => writes.length).toBe(3);
    for (const field of [name, description, logo]) await expect(field).toBeDisabled();
    await expect(dialog.getByRole("button", { name: messages.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    releaseWrite();
    await expect.poll(() => refreshing).toBe(true);
    await expect(name).toBeDisabled();
    await expect(dialog.getByRole("button", { name: messages.catalog.saving, exact: true })).toBeDisabled();
    releaseRefresh();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole("heading", { name: "Harvest Co" })).toBeVisible();
    expect(writes).toHaveLength(3);
    expect(writes.every(body => body.brandId === brand.id && body.name === "Harvest Co" && body.description === "Updated farms" && body.logoUrl === brand.logoUrl)).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("brand creator cannot open an existing brand editor", async ({ page }) => {
  const permissions = ["Permissions.Catalog.Brands.View", "Permissions.Catalog.Brands.Create"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const methods: string[] = [];
  await page.route("**/api/v1/catalog/brands**", route => {
    methods.push(route.request().method());
    return route.fulfill({ json: paged([brand]) });
  });
  await page.goto("/catalog/brands");
  await expect(page.getByRole("heading", { name: brand.name })).toBeVisible();
  await expect(page.getByRole("button", { name: "New brand", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit", exact: true })).toHaveCount(0);
  expect(methods.every(method => method === "GET")).toBe(true);
});
