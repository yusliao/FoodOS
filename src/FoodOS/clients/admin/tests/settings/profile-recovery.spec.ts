import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { ADMIN_PROFILE, installAdminShellMocks } from "../helpers/shell-mocks";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
  await installAdminShellMocks(page, []);
});

test("profile loading does not expose editable controls", async ({ page }) => {
  let release: (() => void) | undefined;
  await page.route("**/api/v1/identity/profile", async route => {
    await new Promise<void>(resolve => { release = resolve; });
    await route.fulfill({ json: ADMIN_PROFILE });
  });
  await page.goto("/settings/profile");
  const main = page.getByRole("main");
  await expect(main.getByText("Loading profile", { exact: true })).toBeVisible();
  await expect(main.getByRole("button", { name: "Save avatar" })).toHaveCount(0);
  release?.();
  await expect(main.getByLabel(/^Username/)).toHaveValue(ADMIN_PROFILE.userName);
});

test("upload denial re-enables controls without saving an avatar", async ({ page }) => {
  const permissions = ["Permissions.Files.Upload"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const writes: string[] = [];
  let release: (() => void) | undefined;
  page.on("request", request => {
    if (request.url().includes("/api/v1/") && request.method() !== "GET") writes.push(request.url());
  });
  await page.route("**/api/v1/files/upload-url", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON().ownerType).toBe("User");
    expect(route.request().postDataJSON().ownerId).toBe(ADMIN_PROFILE.id);
    await new Promise<void>(resolve => { release = resolve; });
    await route.fulfill({ status: 403, json: { detail: "Upload denied" } });
  });
  await page.goto("/settings/profile");
  const main = page.getByRole("main");
  const upload = main.getByRole("button", { name: "Choose image", exact: true });
  const chooser = page.waitForEvent("filechooser");
  await upload.click();
  await (await chooser).setFiles({ name: "avatar.png", mimeType: "image/png", buffer: Buffer.from([137, 80, 78, 71]) });
  await expect.poll(() => !!release).toBe(true);
  await expect(upload).toBeDisabled();
  await expect(main.getByRole("button", { name: "Paste URL" })).toBeDisabled();
  await expect(main.getByRole("button", { name: "Save avatar" })).toBeDisabled();
  release?.();
  await expect(page.getByText("Upload denied", { exact: true })).toBeVisible();
  await expect(upload).toBeEnabled();
  expect(writes.filter(url => !url.endsWith("/files/upload-url"))).toEqual([]);
});

for (const status of [403, 500]) {
  test(`profile ${status} can retry without user-management permissions`, async ({ page }) => {
    let failed = true;
    await page.route("**/api/v1/identity/profile", route => route.fulfill({
      status: failed ? status : 200,
      contentType: "application/json",
      body: JSON.stringify(failed ? { detail: "Profile temporarily unavailable" } : ADMIN_PROFILE),
    }));
    await page.goto("/settings/profile");
    await expect(page.getByRole("main").getByText("Profile temporarily unavailable")).toBeVisible();
    failed = false;
    await page.getByRole("button", { name: "Retry", exact: true }).click();
    await expect(page.getByLabel(/^Username/)).toHaveValue(ADMIN_PROFILE.userName);
  });
}

for (const culture of ["en-US", "zh-CN"]) {
  test(`avatar draft, failed save, retry and clear without Files.Upload · ${culture}`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.addInitScript(c => localStorage.setItem("foodos.culture", c), culture);
    const writes: (string | null)[] = [];
    const fileRequests: string[] = [];
    let imageUrl: string | null = null;
    let release: (() => void) | undefined;
    page.on("request", request => {
      if (request.url().includes("/api/v1/files")) fileRequests.push(request.url());
    });
    await page.route("**/api/v1/identity/profile", route => route.fulfill({
      json: { ...ADMIN_PROFILE, imageUrl },
    }));
    await page.route("**/api/v1/identity/profile/image", async route => {
      expect(route.request().headers().tenant).toBe("root");
      writes.push(route.request().postDataJSON().imageUrl);
      if (writes.length === 1) {
        await new Promise<void>(resolve => { release = resolve; });
        await route.fulfill({ status: 403, json: { detail: "Avatar save denied" } });
      } else {
        imageUrl = writes.at(-1) ?? null;
        await route.fulfill({ status: 204 });
      }
    });
    await page.goto("/settings/profile");
    const main = page.getByRole("main");
    const input = main.getByRole("textbox", { name: culture === "en-US" ? "Image URL" : "图片链接", exact: true });
    const save = main.getByRole("button", { name: culture === "en-US" ? "Save avatar" : "保存头像", exact: true });
    await expect(input).toBeVisible();
    await expect(main.getByRole("button", { name: /Choose image|选择图片/ })).toHaveCount(0);
    await expect(save).toBeDisabled();
    const url = "https://example.test/avatar.png";
    await input.pressSequentially(url);
    expect(writes).toEqual([]);
    await save.click();
    await expect.poll(() => writes.length).toBe(1);
    await expect(input).toBeDisabled();
    await expect(main.getByRole("button", { name: culture === "en-US" ? "Saving avatar…" : "正在保存头像…", exact: true })).toBeDisabled();
    release?.();
    await expect(page.getByText("Avatar save denied", { exact: true })).toBeVisible();
    await expect(input).toHaveValue(url);
    await save.click();
    await expect(save).toBeDisabled();
    await expect(input).toBeEnabled();
    await input.fill("");
    await save.click();
    await expect.poll(() => writes).toEqual([url, url, null]);
    await expect(input).toBeEnabled();
    expect(fileRequests).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
