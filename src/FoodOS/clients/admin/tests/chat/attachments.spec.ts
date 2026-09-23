import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const messageId = "44444444-4444-4444-4444-444444444444";
const fileId = "77777777-7777-7777-7777-777777777777";
const view = "Permissions.Chat.Channels.View";
const send = "Permissions.Chat.Messages.Send";
const upload = "Permissions.Files.Upload";
const channel = { id: channelId, type: "Channel", name: "Operator team", isPrivate: true, unreadCount: 0, members: [{ userId: TEST_USER.sub }] };
const attachment = { id: "attachment-1", fileAssetId: fileId, url: "https://untrusted.invalid/stale", originalFileName: "evidence.pdf", contentType: "application/pdf", sizeBytes: 4 };
const message = { id: messageId, channelId, body: "See evidence", parentMessageId: null, authorUserId: TEST_USER.sub, replyCount: 0, createdAtUtc: "2026-09-18T00:00:00Z", attachments: [attachment], reactions: [] };

async function setup(page: Page, permissions: string[], messages = [message]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: messages }));
}

test("readonly member requests a fresh attachment URL only on click and retries 403", async ({ page }) => {
  await setup(page, [view]);
  const fileRequests: string[] = [];
  let denied = true;
  await page.route(`**/api/v1/files/${fileId}/url`, route => {
    expect(route.request().headers().tenant).toBe("root");
    fileRequests.push(route.request().url());
    return route.fulfill(denied
      ? { status: 403, json: { detail: "Download denied" } }
      : { json: { url: "https://storage.invalid/download", expiresAt: "2099-09-18T00:00:00Z" } });
  });
  const external: string[] = [];
  page.on("request", request => { if (request.url().includes("untrusted.invalid")) external.push(request.url()); });
  await page.goto(`/chat/${channelId}`);
  await expect(page.getByText(attachment.originalFileName)).toBeVisible();
  expect(fileRequests).toEqual([]);
  expect(external).toEqual([]);
  await page.getByRole("button", { name: en.chat.prepareDownload }).click();
  await expect(page.getByText("Download denied")).toBeVisible();
  denied = false;
  await page.getByRole("button", { name: en.chat.prepareDownload }).click();
  const link = page.getByRole("link", { name: en.chat.download });
  await expect(link).toHaveAttribute("href", "https://storage.invalid/download");
  expect(fileRequests).toHaveLength(2);
  await expect(page.getByLabel(en.chatCompose.attachment)).toHaveCount(0);
});

test("message sender without Files.Upload never exposes upload control or calls Files", async ({ page }) => {
  await setup(page, [view, send], []);
  const files: string[] = [];
  page.on("request", request => { if (request.url().includes("/api/v1/files/")) files.push(request.url()); });
  await page.goto(`/chat/${channelId}`);
  await expect(page.getByRole("form", { name: en.chatCompose.message })).toBeVisible();
  await expect(page.getByLabel(en.chatCompose.attachment)).toHaveCount(0);
  expect(files).toEqual([]);
});

for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} private upload finalizes before sending attachment on mobile`, async ({ page }) => {
    await setup(page, [view, send, upload], []);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    const steps: string[] = [];
    await page.route("**/api/v1/files/upload-url", route => {
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().postDataJSON()).toEqual({ ownerType: "ChatChannel", ownerId: channelId, fileName: "evidence.pdf", contentType: "application/pdf", sizeBytes: 4, visibility: "Private", category: "Document" });
      steps.push("request");
      return route.fulfill({ json: { fileAssetId: fileId, uploadUrl: "https://storage.invalid/upload", requiredHeaders: { "Content-Type": "application/pdf" }, expiresAt: "2099-09-18T00:00:00Z" } });
    });
    await page.route("https://storage.invalid/upload", route => { steps.push("put"); return route.fulfill({ status: 200, headers: { "Access-Control-Allow-Origin": "*" } }); });
    await page.route(`**/api/v1/files/${fileId}/finalize`, route => {
      steps.push("finalize");
      return route.fulfill({ json: { id: fileId, ownerType: "ChatChannel", ownerId: channelId, originalFileName: "evidence.pdf", contentType: "application/pdf", sizeBytes: 4, visibility: "Private", status: "Available", createdAtUtc: "2026-09-18T00:00:00Z", createdByUserId: TEST_USER.sub } });
    });
    await page.route(`**/api/v1/files/${fileId}/url?inline=true`, route => { steps.push("url"); return route.fulfill({ json: { url: "https://storage.invalid/read", expiresAt: "2099-09-18T00:00:00Z" } }); });
    let sent: Record<string, unknown> | undefined;
    await page.route(`**/api/v1/chat/channels/${channelId}/messages`, route => {
      steps.push("send");
      sent = route.request().postDataJSON();
      return route.fulfill({ json: { ...message, id: "88888888-8888-8888-8888-888888888888", body: null, attachments: [{ ...attachment, url: "https://storage.invalid/read" }] } });
    });
    await page.goto(`/chat/${channelId}`);
    const form = page.getByRole("form", { name: m.chatCompose.message });
    await form.getByLabel(m.chatCompose.attachment).setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
    await expect(form.getByText(/evidence\.pdf/)).toBeVisible();
    await form.getByRole("button", { name: m.chatCompose.send }).click();
    await expect.poll(() => steps).toEqual(["request", "put", "finalize", "url", "send"]);
    expect(sent).toEqual({ body: "", parentMessageId: null, attachments: [{ fileAssetId: fileId, url: "https://storage.invalid/read", contentType: "application/pdf", fileName: "evidence.pdf", sizeBytes: 4 }] });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("upload 403 shows retry and never sends or starts storage PUT", async ({ page }) => {
  await setup(page, [view, send, upload], []);
  const steps: string[] = [];
  await page.route("**/api/v1/files/upload-url", route => { steps.push("request"); return route.fulfill({ status: 403, json: { detail: "Upload denied" } }); });
  await page.goto(`/chat/${channelId}`);
  const form = page.getByRole("form", { name: en.chatCompose.message });
  await form.getByLabel(en.chatCompose.attachment).setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
  await expect(form.getByText("Upload denied")).toBeVisible();
  await expect(form.getByRole("button", { name: en.chatCompose.retryUpload })).toBeVisible();
  await expect(form.getByRole("button", { name: en.chatCompose.send })).toBeDisabled();
  expect(steps).toEqual(["request"]);
});
