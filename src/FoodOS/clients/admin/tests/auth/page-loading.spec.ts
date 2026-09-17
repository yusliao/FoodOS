import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

for (const [culture, messages] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} lazy page loading is localized and resolves on mobile`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
    await installAdminShellMocks(page, []);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let release: (() => void) | undefined;
    await page.route("**/src/pages/settings/appearance.tsx", async route => {
      await new Promise<void>(resolve => { release = resolve; });
      await route.continue();
    });
    await page.goto("/settings/appearance");
    await expect.poll(() => !!release).toBe(true);
    const status = page.getByRole("main").getByRole("status");
    await expect(status).toHaveText(messages.common.loading);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    release?.();
    await expect(status).toHaveCount(0);
    await expect(page.getByRole("main").getByRole("button", { pressed: true, name: new RegExp(culture) })).toBeVisible();
  });

  test(`${culture} lazy module failure offers a working full reload`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
    await installAdminShellMocks(page, []);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    let fail = true;
    await page.route("**/src/pages/settings/appearance.tsx", route => fail ? route.abort() : route.continue());
    await page.goto("/settings/appearance");
    const reload = page.getByRole("button", { name: messages.common.reload, exact: true });
    await expect(reload).toBeVisible();
    fail = false;
    await reload.click();
    await expect(page.getByRole("main").getByRole("button", { pressed: true, name: new RegExp(culture) })).toBeVisible();
    await expect(page).toHaveURL(/\/settings\/appearance$/);
  });
}
