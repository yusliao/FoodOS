import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, ADMIN_PERMS } from "../helpers/shell-mocks";

// AppearanceSettings is purely client-side: theme and accent choices are
// applied to <html> and stored locally. No API to mock beyond the shell.

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [...ADMIN_PERMS] });
  await installAdminShellMocks(page);
});

test.describe("settings · appearance", () => {
  test("renders the Light and Dark theme options", async ({ page }) => {
    await page.goto("/settings/appearance");

    const main = page.getByRole("main");
    // "Theme" prose appears in the section description AND the settings nav
    // link ("Theme and visual preferences"); anchor to the SettingsSection <h2>.
    await expect(main.getByRole("heading", { name: "Theme" })).toBeVisible({ timeout: 10_000 });
    // Each mode is an aria-pressed <button>. The accessible name is built from
    // the label + blurb (+ "Active" when selected), so match the label as a
    // substring rather than anchoring to the start.
    await expect(main.getByRole("button", { name: /Light/ })).toBeVisible();
    await expect(main.getByRole("button", { name: /Dark/ })).toBeVisible();
  });

  test("selecting Dark applies dark mode and Light reverts it", async ({ page }) => {
    await page.goto("/settings/appearance");

    const main = page.getByRole("main");
    const dark = main.getByRole("button", { name: /Dark/ });
    const light = main.getByRole("button", { name: /Light/ });
    await expect(dark).toBeVisible({ timeout: 10_000 });

    // Force a known starting point regardless of the system color-scheme,
    // then assert each toggle drives the <html> class.
    await dark.click();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await expect(dark).toHaveAttribute("aria-pressed", "true");

    await light.click();
    await expect(page.locator("html")).not.toHaveClass(/dark/);
    await expect(light).toHaveAttribute("aria-pressed", "true");
  });

  test("persists the selected theme to localStorage", async ({ page }) => {
    await page.goto("/settings/appearance");

    const main = page.getByRole("main");
    const light = main.getByRole("button", { name: /Light/ });
    await expect(light).toBeVisible({ timeout: 10_000 });

    await light.click();
    await expect(page.locator("html")).not.toHaveClass(/dark/);

    const stored = await page.evaluate(() => localStorage.getItem("fsh.admin.theme"));
    expect(stored).toBe("light");
  });

  test("switches accent palettes and restores the selection after reload", async ({ page }) => {
    await page.goto("/settings/appearance");

    const main = page.getByRole("main");
    const indigo = main.getByRole("button", { name: "Indigo", exact: true });
    const amber = main.getByRole("button", { name: "Amber", exact: true });
    const rose = main.getByRole("button", { name: "Rose", exact: true });
    await expect(indigo).toBeVisible({ timeout: 10_000 });

    await indigo.click();
    await expect(indigo).toHaveAttribute("aria-pressed", "true");
    await expect(page.locator("html")).toHaveClass(/accent-indigo/);
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--brand-600").trim()))
      .toBe("oklch(0.555 0.220 268)");

    await amber.click();
    await expect(page.locator("html")).toHaveClass(/accent-amber/);
    await expect(page.locator("html")).not.toHaveClass(/accent-indigo/);
    await page.reload();
    await expect(amber).toHaveAttribute("aria-pressed", "true");
    await expect(page.locator("html")).toHaveClass(/accent-amber/);
    expect(await page.evaluate(() => localStorage.getItem("fsh.admin.accent"))).toBe("amber");

    await rose.click();
    await expect(rose).toHaveAttribute("aria-pressed", "true");
    await expect(page.locator("html")).not.toHaveClass(/accent-/);
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--brand-600").trim()))
      .toMatch(/^oklch\(0\.575\s+0\.232\s+13\)$/);
  });
});
