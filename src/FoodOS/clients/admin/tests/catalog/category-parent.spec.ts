import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const view = "Permissions.Catalog.Categories.View";
const category = { id: "22222222-2222-2222-2222-222222222222", name: "Leafy greens", slug: "leafy-greens", parentCategoryId: "old-parent" };
for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const mode of ["create", "edit"] as const) test(`${culture} ${mode} parent choices are independent of list search and preserve server errors`, async ({ page }) => {
    const permissions = [view, `Permissions.Catalog.Categories.${mode === "create" ? "Create" : "Update"}`];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let secondFailed = false;
    const reads: string[] = [];
    await page.route("**/api/v1/catalog/categories?**", route => {
      expect(route.request().headers().tenant).toBe("root");
      const url = new URL(route.request().url());
      reads.push(url.search);
      const pageNumber = Number(url.searchParams.get("pageNumber"));
      if (url.searchParams.get("search") === "leafy") return route.fulfill({ json: paged([category]) });
      if (url.searchParams.get("search")) return route.fulfill({ json: paged([]) });
      if (pageNumber === 2 && !secondFailed) {
        secondFailed = true;
        return route.fulfill({ status: 403, json: {} });
      }
      return route.fulfill({ json: paged(pageNumber === 1 ? [category] : [{ id: "later-parent", name: "Later parent" }], { pageNumber, pageSize: 50, totalCount: 51, totalPages: 2 }) });
    });
    const writes: Record<string, unknown>[] = [];
    await page.route(mode === "create" ? "**/api/v1/catalog/categories" : `**/api/v1/catalog/categories/${category.id}`, route => {
      expect(route.request().headers().tenant).toBe("root");
      writes.push(route.request().postDataJSON());
      return route.fulfill(writes.length === 1 ? { status: 400, json: { detail: "Setting this parent would create a cycle." } } : { json: category.id });
    });
    await page.goto("/catalog/categories");
    await page.getByRole("searchbox").fill("leafy");
    await page.getByRole("button", { name: messages.catalog.search, exact: true }).click();
    await expect.poll(() => reads.some(value => value.includes("search=leafy"))).toBe(true);
    await page.getByRole("button", { name: mode === "create" ? messages.catalog.categories.new : messages.catalog.edit, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("textbox", { name: messages.catalog.name, exact: true }).fill("Updated name");
    const choice = dialog.getByRole("region", { name: messages.catalog.categories.parent, exact: true });
    const select = choice.getByRole("combobox");
    await expect(select).toBeEnabled();
    if (mode === "edit") {
      await expect(select).toHaveValue("old-parent");
      await expect(select.locator("option:checked")).toHaveText("old-parent");
      await expect(select.locator(`option[value="${category.id}"]`)).toHaveCount(0);
    }
    await choice.getByRole("button", { name: messages.common.next, exact: true }).click();
    await expect(choice.getByRole("alert")).toBeVisible();
    await choice.getByRole("button", { name: messages.workbench.retry, exact: true }).click();
    await expect(select.getByRole("option", { name: "Later parent" })).toBeAttached();
    await select.selectOption("later-parent");
    await choice.getByRole("searchbox").fill("missing");
    await expect(choice.getByRole("status")).toHaveText(messages.common.emptyDefault);
    await expect(select).toHaveValue("later-parent");
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText("Setting this parent would create a cycle.");
    await expect(select).toHaveValue("later-parent");
    await expect(dialog.getByRole("textbox", { name: messages.catalog.name, exact: true })).toHaveValue("Updated name");
    await select.selectOption("");
    await dialog.getByRole("button", { name: messages.catalog.save, exact: true }).click();
    await expect(dialog).toHaveCount(0);
    expect(writes).toHaveLength(2);
    expect(writes[0]).toMatchObject({ name: "Updated name", parentCategoryId: "later-parent" });
    expect(writes[1]).toMatchObject({ name: "Updated name", parentCategoryId: null });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("category reader cannot open a parent picker or send writes", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [view] });
  await installAdminShellMocks(page, [view]);
  const methods: string[] = [];
  await page.route("**/api/v1/catalog/categories**", route => {
    methods.push(route.request().method());
    return route.fulfill({ json: paged([category]) });
  });
  await page.goto("/catalog/categories");
  await expect(page.getByRole("heading", { name: category.name })).toBeVisible();
  await expect(page.getByRole("button", { name: /New category|Edit|Delete/ })).toHaveCount(0);
  await expect(page.getByRole("combobox")).toHaveCount(0);
  expect(methods.every(method => method === "GET")).toBe(true);
});
