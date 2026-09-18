import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const parentId = "44444444-4444-4444-4444-444444444444";
const replyId = "55555555-5555-5555-5555-555555555555";
const sentId = "66666666-6666-6666-6666-666666666666";
const view = "Permissions.Chat.Channels.View", send = "Permissions.Chat.Messages.Send";
const channel = { id: channelId, type: "Channel", name: "Operator team", isPrivate: true, unreadCount: 0, members: [{ userId: TEST_USER.sub }] };
const message = { id: parentId, channelId, body: "Parent without replies", parentMessageId: null, authorUserId: TEST_USER.sub, replyCount: 0, createdAtUtc: "2026-09-17T00:00:00Z", attachments: [], reactions: [] };
async function setup(page: Page, permissions = [view, send]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [message] }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages/${parentId}`, route => route.fulfill({ json: message }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages/${replyId}`, route => route.fulfill({ json: { ...message, id: replyId, body: "Selected reply", parentMessageId: parentId } }));
  await page.route("**/api/v1/chat/messages/*/replies?**", route => route.fulfill({ json: [] }));
}
for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const threaded of [false, true]) test(`${culture} ${threaded ? "reply" : "channel"} retries unchanged text and freezes through refresh`, async ({ page }) => {
    await setup(page);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    const writes: { body: unknown; key: string }[] = [];
    let releaseWrite!: () => void, releaseRefresh!: () => void;
    const writeHold = new Promise<void>(resolve => { releaseWrite = resolve; });
    const refreshHold = new Promise<void>(resolve => { releaseRefresh = resolve; });
    let saved = false;
    const sent = { ...message, id: sentId, body: "Delivery checked", parentMessageId: threaded ? parentId : null };
    await page.route(`**/api/v1/chat/channels/${channelId}/messages/${sentId}`, route => route.fulfill({ json: sent }));
    await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, async route => {
      if (saved) await refreshHold;
      return route.fulfill({ json: saved && !threaded ? [sent, message] : [message] });
    });
    await page.route(`**/api/v1/chat/channels/${channelId}/messages`, async route => {
      expect(route.request().method()).toBe("POST");
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().headers()["accept-language"]).toBe(culture);
      writes.push({ body: route.request().postDataJSON(), key: route.request().headers()["idempotency-key"] });
      if (writes.length === 1) return route.fulfill({ status: 403, json: { detail: "Send denied" } });
      await writeHold;
      saved = true;
      return route.fulfill({ json: sent });
    });
    await page.goto(`/chat/${channelId}${threaded ? `?messageId=${replyId}` : ""}`);
    const form = page.getByRole("form", { name: threaded ? m.chatCompose.reply : m.chatCompose.message, exact: true });
    const input = form.getByRole("textbox");
    await expect(form.getByRole("button", { name: m.chatCompose.send })).toBeDisabled();
    await input.fill("  Delivery checked  ");
    await form.getByRole("button", { name: m.chatCompose.send }).click();
    await expect(form.getByText("Send denied")).toBeVisible();
    await expect(input).toHaveValue("  Delivery checked  ");
    await form.getByRole("button", { name: m.chatCompose.send }).click();
    await expect(input).toBeDisabled();
    await expect(page.getByRole("textbox", { name: m.chatCompose.message, exact: true })).toBeDisabled();
    if (threaded) await expect(page.getByRole("button", { name: m.chat.closeTarget })).toBeDisabled();
    releaseWrite();
    await expect.poll(() => saved).toBe(true);
    await expect(input).toBeDisabled();
    await expect(input).toHaveValue("  Delivery checked  ");
    releaseRefresh();
    await expect(page).toHaveURL(new RegExp(`messageId=${sentId}$`));
    await expect(page.getByRole("region", { name: m.chat.target, exact: true }).getByText("Delivery checked", { exact: true })).toBeVisible();
    await expect(page.getByRole("textbox", { name: m.chatCompose.message, exact: true })).toHaveValue("");
    expect(writes).toHaveLength(2);
    expect(writes[0].key).toBeTruthy();
    expect(writes[1]).toEqual(writes[0]);
    expect(writes[0].body).toEqual({ body: "Delivery checked", parentMessageId: threaded ? parentId : null, attachments: [] });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("changing text after failure uses a new key; empty input never posts", async ({ page }) => {
  await setup(page);
  const keys: string[] = [], bodies: string[] = [];
  await page.route(`**/api/v1/chat/channels/${channelId}/messages`, route => {
    keys.push(route.request().headers()["idempotency-key"]);
    bodies.push(route.request().postDataJSON().body);
    return route.fulfill({ status: 409, json: { detail: "Try again explicitly" } });
  });
  await page.goto(`/chat/${channelId}`);
  const form = page.getByRole("form", { name: en.chatCompose.message, exact: true });
  await form.getByRole("textbox").fill("   ");
  await expect(form.getByRole("button")).toBeDisabled();
  expect(keys).toEqual([]);
  await form.getByRole("textbox").fill("first");
  await form.getByRole("button").click();
  await expect(form.getByText("Try again explicitly")).toBeVisible();
  await form.getByRole("textbox").fill("second");
  await form.getByRole("button").click();
  await expect.poll(() => keys.length).toBe(2);
  expect(keys[1]).not.toBe(keys[0]);
  expect(bodies).toEqual(["first", "second"]);
  await expect(form.getByRole("textbox")).toHaveAttribute("maxlength", "32768");
});

test("thread can start at zero replies and readonly member has no composer", async ({ page }) => {
  await setup(page, [view]);
  const writes: string[] = [];
  page.on("request", request => { if (request.url().includes("/chat/") && request.method() !== "GET") writes.push(request.url()); });
  await page.goto(`/chat/${channelId}`);
  await page.getByRole("link", { name: "View thread (0)", exact: true }).click();
  await expect(page.getByRole("region", { name: en.chat.target, exact: true }).getByText(message.body)).toBeVisible();
  await expect(page.getByRole("textbox")).toHaveCount(0);
  expect(writes).toEqual([]);
});

test("Send cannot expose composer for a public channel nonmember", async ({ page }) => {
  await setup(page);
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: { ...channel, isPrivate: false, members: [] } }));
  await page.goto(`/chat/${channelId}?messageId=${replyId}`);
  await expect(page.getByText(en.chat.notMember)).toBeVisible();
  await expect(page.getByRole("textbox")).toHaveCount(0);
});

test("leaving during send does not navigate back on a late successful response", async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const hold = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/chat/channels?**", route => route.fulfill({ json: [channel] }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages`, async route => { await hold; return route.fulfill({ json: { ...message, id: sentId } }); });
  await page.goto(`/chat/${channelId}`);
  await page.getByRole("textbox", { name: en.chatCompose.message, exact: true }).fill("Leaving now");
  await page.getByRole("button", { name: en.chatCompose.send, exact: true }).click();
  await expect(page.getByRole("textbox")).toBeDisabled();
  await page.getByRole("link", { name: en.chat.back }).click();
  await expect(page.getByRole("heading", { name: en.chat.title, exact: true })).toBeVisible();
  const response = page.waitForResponse(value => value.request().method() === "POST" && value.url().endsWith(`/channels/${channelId}/messages`));
  release();
  await response;
  await expect(page).toHaveURL(/\/chat$/);
  await expect(page.getByRole("heading", { name: en.chat.title, exact: true })).toBeVisible();
});
