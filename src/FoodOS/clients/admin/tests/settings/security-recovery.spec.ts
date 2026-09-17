import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { ADMIN_PROFILE, installAdminShellMocks } from "../helpers/shell-mocks";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
  await installAdminShellMocks(page, []);
});

test("security profile denial can be retried without management permissions", async ({ page }) => {
  let fail = true;
  await page.route("**/api/v1/identity/profile", route => route.fulfill({
    status: fail ? 403 : 200, json: fail ? { detail: "Security unavailable" } : ADMIN_PROFILE,
  }));
  await page.goto("/settings/security");
  await expect(page.getByRole("main").getByText("Security unavailable")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: "Change password", exact: true })).toBeVisible();
});

for (const culture of ["en-US", "zh-CN"]) {
  test(`password request freezes dialog, failure preserves inputs and success clears · ${culture}`, async ({ page }) => {
    await page.addInitScript(c => localStorage.setItem("foodos.culture", c), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    const names = culture === "en-US" ? { open: "Change password", save: "Update password", close: "Close" }
      : { open: "修改密码", save: "更新密码", close: "关闭" };
    let calls = 0;
    let release: (() => void) | undefined;
    await page.route("**/api/v1/identity/change-password", async route => {
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().postDataJSON()).toEqual({ password: "OldPass123!", newPassword: "NewPass456!", confirmNewPassword: "NewPass456!" });
      calls++;
      if (calls === 1) {
        await new Promise<void>(resolve => { release = resolve; });
        await route.fulfill({ status: 400, json: { detail: "Password change rejected" } });
      } else await route.fulfill({ json: "Password changed." });
    });
    await page.goto("/settings/security");
    await page.getByRole("button", { name: names.open, exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.locator("#pw-current").fill("OldPass123!");
    await dialog.locator("#pw-next").fill("NewPass456!");
    await dialog.locator("#pw-confirm").fill("NewPass456!");
    await dialog.getByRole("button", { name: names.save, exact: true }).click();
    await expect.poll(() => !!release).toBe(true);
    await expect(dialog.locator("#pw-current")).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: names.close, exact: true }).click();
    await expect(dialog).toBeVisible();
    release?.();
    await expect(page.getByText("Password change rejected", { exact: true })).toBeVisible();
    await expect(dialog.locator("#pw-current")).toHaveValue("OldPass123!");
    await dialog.getByRole("button", { name: names.save, exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await page.getByRole("button", { name: names.open, exact: true }).click();
    await expect(dialog.locator("#pw-current")).toHaveValue("");
    expect(calls).toBe(2);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("2FA enrollment retries errors, protects verification and refresh, then disables", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  let enabled = false;
  let enrollCalls = 0;
  let verifyCalls = 0;
  let disableCalls = 0;
  let releaseVerify: (() => void) | undefined;
  let releaseProfile: (() => void) | undefined;
  let releaseDisable: (() => void) | undefined;
  await page.route("**/api/v1/identity/profile", async route => {
    if (enabled && verifyCalls === 2 && disableCalls === 0) await new Promise<void>(resolve => { releaseProfile = resolve; });
    await route.fulfill({ json: { ...ADMIN_PROFILE, twoFactorEnabled: enabled } });
  });
  await page.route("**/api/v1/identity/2fa/**", async route => {
    expect(route.request().headers().tenant).toBe("root");
    if (route.request().url().endsWith("/enroll")) {
      enrollCalls++;
      await route.fulfill(enrollCalls === 1 ? { status: 500, json: { detail: "Enrollment unavailable" } }
        : { json: { sharedKey: "TESTKEY", authenticatorUri: "otpauth://totp/FoodOS:test?secret=JBSWY3DPEHPK3PXP&issuer=FoodOS&digits=6" } });
    } else if (route.request().url().endsWith("/verify")) {
      verifyCalls++;
      expect(route.request().postDataJSON()).toEqual({ code: "123456" });
      if (verifyCalls === 1) await route.fulfill({ status: 400, json: { detail: "Invalid code" } });
      else {
        await new Promise<void>(resolve => { releaseVerify = resolve; });
        enabled = true;
        await route.fulfill({ json: { success: true } });
      }
    } else {
      disableCalls++;
      expect(route.request().postDataJSON()).toEqual({ currentPassword: "CurrentPass123!" });
      if (disableCalls === 1) await route.fulfill({ status: 403, json: { detail: "Disable denied" } });
      else {
        await new Promise<void>(resolve => { releaseDisable = resolve; });
        enabled = false;
        await route.fulfill({ json: { success: true } });
      }
    }
  });
  await page.goto("/settings/security");
  const main = page.getByRole("main");
  const enroll = main.getByRole("button", { name: "Enable two-factor", exact: true });
  await enroll.click();
  await expect(page.getByText("Enrollment unavailable", { exact: true })).toBeVisible();
  await enroll.click();
  await expect(main.getByRole("img")).toBeVisible();
  const otp = main.locator("#totp-code");
  const verify = main.getByRole("button", { name: "Confirm & enable", exact: true });
  await expect(verify).toBeDisabled();
  await otp.fill("123 456");
  await verify.click();
  await expect(page.getByText("Invalid code", { exact: true })).toBeVisible();
  await expect(otp).toHaveValue("123456");
  await verify.click();
  await expect.poll(() => !!releaseVerify).toBe(true);
  await expect(otp).toBeDisabled();
  await expect(main.getByRole("button", { name: "Cancel", exact: true })).toBeDisabled();
  await expect(main.getByRole("button", { name: "Change password", exact: true })).toBeDisabled();
  releaseVerify?.();
  await expect.poll(() => !!releaseProfile).toBe(true);
  await expect(otp).toBeDisabled();
  releaseProfile?.();
  const pw = main.locator("#disable-pw");
  await expect(pw).toBeVisible();
  await pw.fill("CurrentPass123!");
  const disable = main.getByRole("button", { name: "Disable two-factor", exact: true });
  await disable.click();
  await expect(main.getByRole("alert")).toContainText("Disable denied");
  await expect(pw).toHaveValue("CurrentPass123!");
  await disable.click();
  await expect.poll(() => !!releaseDisable).toBe(true);
  await expect(pw).toBeDisabled();
  releaseDisable?.();
  await expect(enroll).toBeEnabled();
  await expect(main.getByText("TESTKEY", { exact: true })).toHaveCount(0);
  expect([enrollCalls, verifyCalls, disableCalls]).toEqual([2, 2, 2]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("canceling enrollment removes the displayed secret and a new attempt requests a new key", async ({ page }) => {
  let calls = 0;
  await page.route("**/api/v1/identity/2fa/enroll", route => {
    calls++;
    return route.fulfill({ json: { sharedKey: `TESTKEY${calls}`, authenticatorUri: "otpauth://totp/test?secret=JBSWY3DPEHPK3PXP" } });
  });
  await page.goto("/settings/security");
  const main = page.getByRole("main");
  const enroll = main.getByRole("button", { name: "Enable two-factor", exact: true });
  await enroll.click();
  await expect(main.getByText("TESTKEY1", { exact: true })).toBeVisible();
  await main.locator("#totp-code").fill("123456");
  await main.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(main.getByText("TESTKEY1", { exact: true })).toHaveCount(0);
  await enroll.click();
  await expect(main.getByText("TESTKEY2", { exact: true })).toBeVisible();
  await expect(main.locator("#totp-code")).toHaveValue("");
  expect(calls).toBe(2);
});

test("wrong 2FA password retries once after refresh, preserves input and permits correction", async ({ page }) => {
  let enabled = true;
  let refreshes = 0;
  const attempts: string[] = [];
  let originalAuth = "";
  let rotatedAuth = "";
  await page.route("**/api/v1/identity/profile", route => route.fulfill({ json: { ...ADMIN_PROFILE, twoFactorEnabled: enabled } }));
  await page.route("**/api/v1/identity/token/refresh", async route => {
    refreshes++;
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON().token).toBe(originalAuth.slice(7));
    const token = originalAuth.slice(7).replace(/sig$/, "rotated-signature");
    rotatedAuth = `Bearer ${token}`;
    await route.fulfill({ json: { token, refreshToken: "rotated-refresh" } });
  });
  await page.route("**/api/v1/identity/2fa/disable", async route => {
    const password = route.request().postDataJSON().currentPassword;
    attempts.push(password);
    expect(route.request().headers().tenant).toBe("root");
    if (attempts.length === 1) originalAuth = route.request().headers().authorization;
    else expect(route.request().headers().authorization).toBe(rotatedAuth);
    if (password === "wrong") await route.fulfill({ status: 401, json: { detail: "Current password is incorrect." } });
    else {
      enabled = false;
      await route.fulfill({ json: { success: true } });
    }
  });
  await page.goto("/settings/security");
  const main = page.getByRole("main");
  const password = main.locator("#disable-pw");
  const disable = main.getByRole("button", { name: "Disable two-factor", exact: true });
  await password.fill("wrong");
  await disable.click();
  await expect(main.getByRole("alert")).toContainText("Current password is incorrect.");
  await expect(password).toHaveValue("wrong");
  expect(attempts).toEqual(["wrong", "wrong"]);
  expect(refreshes).toBe(1);
  await password.fill("CorrectPass123!");
  await disable.click();
  await expect(main.getByRole("button", { name: "Enable two-factor", exact: true })).toBeEnabled();
  expect(attempts).toEqual(["wrong", "wrong", "CorrectPass123!"]);
  expect(refreshes).toBe(1);
});
