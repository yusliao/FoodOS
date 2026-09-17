import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

for (const culture of ["en-US", "zh-CN"]) {
  test(`QR failure recovers locally and clipboard denial explains manual fallback · ${culture}`, async ({ page }) => {
    const t = culture === "en-US" ? en.settings : zh.settings;
    await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
    await installAdminShellMocks(page, []);
    await page.addInitScript(c => {
      localStorage.setItem("foodos.culture", c);
      Object.defineProperty(navigator, "clipboard", { value: { writeText: async () => { throw new Error("Clipboard denied"); } } });
    }, culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let enrolls = 0;
    const secret = "TESTKEY".repeat(10);
    await page.route("**/api/v1/identity/2fa/enroll", route => {
      enrolls++;
      return route.fulfill({ json: { sharedKey: secret, authenticatorUri: "otpauth://totp/test?secret=TESTKEY" } });
    });
    // Exercise the real effect's rejected generator promise, then local retry.
    // This fixture does not prove QR encoding; the normal enrollment suite loads the real library.
    await page.route("**/node_modules/.vite/deps/qrcode.js*", route => route.fulfill({
      contentType: "application/javascript",
      body: 'let attempts = 0; export default { toString: async () => { if (++attempts === 1) throw new Error("Render failed"); return \'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20"><rect width="20" height="20"/></svg>\'; } };',
    }));
    await page.goto("/settings/security");
    const main = page.getByRole("main");
    await main.getByRole("button", { name: t.enableTwoFa, exact: true }).click();
    await expect(main.getByRole("alert")).toHaveText(t.qrFailed);
    await expect(main.getByText(secret, { exact: true })).toBeVisible();
    await expect(main.getByText(t.rendering, { exact: true })).toHaveCount(0);
    await main.getByRole("button", { name: t.copy, exact: true }).click();
    await expect(page.getByText(t.copyKeyFailed, { exact: true })).toBeVisible();
    await expect(main.getByRole("button", { name: t.copied, exact: true })).toHaveCount(0);
    await main.getByRole("button", { name: t.retryQr, exact: true }).click();
    await expect(main.getByRole("img", { name: t.qrLabel })).toBeVisible();
    await expect(main.getByText(t.qrFailed, { exact: true })).toHaveCount(0);
    expect(enrolls).toBe(1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
