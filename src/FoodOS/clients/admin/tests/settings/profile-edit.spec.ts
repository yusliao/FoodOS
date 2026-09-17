import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { ADMIN_PROFILE, installAdminShellMocks } from "../helpers/shell-mocks";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
  await installAdminShellMocks(page, []);
});

for (const culture of ["en-US", "zh-CN"]) {
  test(`self profile edits persist only supported fields · ${culture}`, async ({ page }) => {
    await page.addInitScript(c => localStorage.setItem("foodos.culture", c), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    const labels = culture === "en-US"
      ? { first: "First name", last: "Last name", phone: "Phone", save: "Save profile", display: "Display name" }
      : { first: "名", last: "姓", phone: "电话", save: "保存资料", display: "显示名称" };
    let profile = { ...ADMIN_PROFILE, firstName: "Root", lastName: "Admin", phoneNumber: "123" };
    const writes: unknown[] = [];
    await page.route("**/api/v1/identity/profile", async route => {
      if (route.request().method() === "PUT") {
        const input = route.request().postDataJSON();
        expect(route.request().headers().tenant).toBe("root");
        writes.push(input);
        profile = { ...profile, ...input };
        await route.fulfill({ status: 200, body: "" });
      } else await route.fulfill({ json: profile });
    });
    await page.goto("/settings/profile");
    const main = page.getByRole("main");
    const save = main.getByRole("button", { name: labels.save, exact: true });
    await expect(save).toBeDisabled();
    await expect(main.locator("#profile-username")).toHaveAttribute("readonly", "");
    await expect(main.locator("#profile-email")).toHaveAttribute("readonly", "");
    await main.getByLabel(labels.first, { exact: true }).fill("Ada");
    await main.getByLabel(labels.last, { exact: true }).fill("Lovelace");
    await main.getByLabel(labels.phone, { exact: true }).fill("+15550142");
    expect(writes).toEqual([]);
    await save.click();
    await expect.poll(() => writes).toEqual([{ firstName: "Ada", lastName: "Lovelace", phoneNumber: "+15550142" }]);
    await expect(main.locator("#profile-display")).toHaveValue("Ada Lovelace");
    await expect(save).toBeDisabled();
    await page.reload();
    await expect(main.getByLabel(labels.first, { exact: true })).toHaveValue("Ada");
    await main.getByLabel(labels.phone, { exact: true }).fill("");
    await save.click();
    await expect.poll(() => writes.length).toBe(2);
    expect(writes[1]).toEqual({ firstName: "Ada", lastName: "Lovelace", phoneNumber: "" });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

for (const status of [403, 500]) {
  test(`profile ${status} preserves edits and serializes avatar writes through refresh`, async ({ page }) => {
    let calls = 0;
    let releaseWrite: (() => void) | undefined;
    let releaseRead: (() => void) | undefined;
    let profile = { ...ADMIN_PROFILE, firstName: "Root" };
    await page.route("**/api/v1/identity/profile", async route => {
      if (route.request().method() === "PUT") {
        calls++;
        if (calls === 1) {
          await new Promise<void>(resolve => { releaseWrite = resolve; });
          await route.fulfill({ status, json: { detail: "Profile save rejected" } });
        } else {
          profile = { ...profile, ...route.request().postDataJSON() };
          await route.fulfill({ status: 200, body: "" });
        }
      } else {
        if (calls === 2) await new Promise<void>(resolve => { releaseRead = resolve; });
        await route.fulfill({ json: profile });
      }
    });
    await page.goto("/settings/profile");
    const main = page.getByRole("main");
    const first = main.getByLabel("First name", { exact: true });
    const save = main.getByRole("button", { name: "Save profile", exact: true });
    const avatar = main.getByLabel("Image URL", { exact: true });
    await avatar.fill("https://example.test/draft.png");
    await first.fill("New name");
    await save.click();
    await expect.poll(() => !!releaseWrite).toBe(true);
    await expect(first).toBeDisabled();
    await expect(avatar).toBeDisabled();
    await expect(main.getByRole("button", { name: "Save avatar" })).toBeDisabled();
    releaseWrite?.();
    await expect(page.getByText("Profile save rejected", { exact: true })).toBeVisible();
    await expect(first).toHaveValue("New name");
    await save.click();
    await expect.poll(() => !!releaseRead).toBe(true);
    await expect(first).toBeDisabled();
    await expect(avatar).toBeDisabled();
    releaseRead?.();
    await expect(first).toBeEnabled();
    await expect(avatar).toHaveValue("https://example.test/draft.png");
    await expect(save).toBeDisabled();
    expect(calls).toBe(2);
  });
}

test("profile input lengths match the server validator", async ({ page }) => {
  await page.goto("/settings/profile");
  for (const [label, length] of [["First name", 50], ["Last name", 50], ["Phone", 15]] as const) {
    const input = page.getByRole("main").getByLabel(label, { exact: true });
    await expect(input).toHaveAttribute("maxlength", String(length));
    await input.fill("a".repeat(length + 5));
    await expect(input).toHaveValue("a".repeat(length));
  }
});
