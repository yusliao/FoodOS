import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const parentId = "44444444-4444-4444-4444-444444444444";
const replyId = "55555555-5555-5555-5555-555555555555";
const view = "Permissions.Chat.Channels.View";
const channel = { id: channelId, type: "Channel", name: "Operator team", isPrivate: true, unreadCount: 2, description: "Current domain only", members: [{ id: "membership", userId: TEST_USER.sub, role: "Member" }] };
const message = { id: parentId, channelId, authorUserId: "operator-author", body: "Earlier parent", parentMessageId: null, replyCount: 1, createdAtUtc: "2026-09-17T00:00:00Z", deletedAtUtc: null, attachments: [], reactions: [] };
const reply = { ...message, id: replyId, body: "Notification reply outside newest page", parentMessageId: parentId, replyCount: 0 };
async function setup(page: Page, permissions = [view]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: string[] = [];
  await page.route("**/api/v1/chat/**", route => {
    const request = route.request();
    expect(request.headers().tenant).toBe("root");
    expect(request.method()).toBe("GET");
    requests.push(new URL(request.url()).pathname);
    return route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } });
  });
  // Record and verify even requests handled by more specific mocks.
  page.on("request", request => {
    if (request.url().includes("/api/v1/chat/")) {
      expect(request.headers().tenant).toBe("root");
      expect(request.method()).toBe("GET");
      requests.push(new URL(request.url()).pathname);
    }
  });
  await page.route("**/api/v1/chat/channels?**", route => route.fulfill({ json: [channel] }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [{ ...message, body: "Latest page message" }] }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages/${parentId}`, route => route.fulfill({ json: message }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages/${replyId}`, route => route.fulfill({ json: reply }));
  await page.route(`**/api/v1/chat/messages/${parentId}/replies?**`, route => route.fulfill({ json: [] }));
  return requests;
}

for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} notification locates old reply and parent in root on mobile without writes`, async ({ page }) => {
    const requests = await setup(page, [view, "Permissions.Notifications.Inbox.View"]);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route("**/api/v1/notifications/?*", route => route.fulfill({ json: [{ id: "notice", title: "Chat mention", type: "chat.mention", source: "Chat", createdAtUtc: message.createdAtUtc, readAtUtc: null, link: `/chat/${channelId}?messageId=${replyId}` }] }));
    await page.goto("/notifications");
    await page.getByRole("main").getByRole("link", { name: m.common.open, exact: true }).click();
    const target = page.getByRole("region", { name: m.chat.target, exact: true });
    await expect(target.getByText(reply.body)).toBeVisible();
    await expect(target.getByRole("region", { name: m.chat.parent }).getByText(message.body)).toBeVisible();
    await expect(target.getByRole("region", { name: m.chat.replies }).getByText(m.chat.noMessages)).toBeVisible();
    await expect(page.getByText(m.chatCompose.browsingHint)).toBeVisible();
    await expect(page.getByRole("textbox")).toHaveCount(0);
    expect(requests.some(path => path.endsWith(`/messages/${replyId}`))).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.getByRole("button", { name: m.chat.closeTarget }).click();
    await expect(target).toHaveCount(0);
    await expect(page).toHaveURL(new RegExp(`/chat/${channelId}$`));
  });
}

test("navigation, failed channel list, paging and empty last page", async ({ page }) => {
  await setup(page);
  let fails = true;
  await page.route("**/api/v1/chat/channels?**", route => {
    if (fails) return route.fulfill({ status: 403, json: { detail: "Channels denied" } });
    const second = new URL(route.request().url()).searchParams.get("page") === "2";
    return route.fulfill({ json: second ? [] : Array.from({ length: 20 }, (_, index) => ({ ...channel, id: index === 0 ? channelId : `channel-${index}`, name: `Channel ${index}` })) });
  });
  await page.goto("/");
  await page.getByRole("button", { name: "Operations", exact: true }).click();
  await page.locator('a[href="/chat"]').first().click();
  await expect(page.getByText("Channels denied")).toBeVisible();
  fails = false;
  await page.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(page.getByRole("link", { name: "Channel 0", exact: true })).toBeVisible();
  await page.getByRole("button", { name: en.common.next, exact: true }).click();
  await expect(page.getByText(en.chat.noChannels)).toBeVisible();
  await expect(page.getByRole("button", { name: en.common.next, exact: true })).toBeDisabled();
  await page.getByRole("button", { name: en.common.previous, exact: true }).click();
  await page.getByRole("link", { name: "Channel 0", exact: true }).click();
  await expect(page.getByRole("heading", { name: channel.name })).toBeVisible();
});

for (const status of [403, 404, "nonmember"] as const) test(`channel ${status} prevents dependent reads`, async ({ page }) => {
  const requests = await setup(page);
  let fails = true;
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill(fails
    ? status === "nonmember" ? { json: { ...channel, isPrivate: false, members: [] } } : { status, json: { detail: "Channel unavailable" } }
    : { json: channel }));
  await page.goto(`/chat/${channelId}?messageId=${replyId}`);
  await expect(page.getByText(status === "nonmember" ? en.chat.notMember : "Channel unavailable")).toBeVisible();
  expect(requests.filter(path => path.includes("/messages"))).toEqual([]);
  if (status !== "nonmember") {
    fails = false;
    await page.getByRole("button", { name: en.workbench.retry, exact: true }).click();
    await expect(page.getByText(reply.body)).toBeVisible();
  }
});

test("target 404 and message list 403 retry independently without premature thread calls", async ({ page }) => {
  const requests = await setup(page);
  let targetFails = true, listFails = true;
  await page.route(`**/api/v1/chat/channels/${channelId}/messages/${replyId}`, route => route.fulfill(targetFails ? { status: 404, json: { detail: "Message unavailable" } } : { json: reply }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill(listFails ? { status: 403, json: { detail: "Messages denied" } } : { json: [] }));
  await page.goto(`/chat/${channelId}?messageId=${replyId}`);
  const target = page.getByRole("region", { name: en.chat.target, exact: true });
  const list = page.getByRole("region", { name: en.chat.messages, exact: true });
  await expect(target.getByText("Message unavailable")).toBeVisible();
  await expect(list.getByText("Messages denied")).toBeVisible();
  expect(requests.filter(path => path.endsWith("/replies"))).toEqual([]);
  targetFails = false;
  await target.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(target.getByText(reply.body)).toBeVisible();
  await expect(list.getByText("Messages denied")).toBeVisible();
  listFails = false;
  await list.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(list.getByText(en.chat.noMessages)).toBeVisible();
});

test("channel and thread use separate older-message cursors and recover failed replies", async ({ page }) => {
  await setup(page);
  const many = Array.from({ length: 20 }, (_, index) => ({ ...message, id: index === 0 ? parentId : `00000000-0000-0000-0000-${String(index + 1).padStart(12, "0")}`, body: `Message ${index}` }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => {
    const before = new URL(route.request().url()).searchParams.get("before");
    if (before) expect(before).toBe(many[19].id);
    return route.fulfill({ json: before ? [] : many });
  });
  let fails = true;
  await page.route(`**/api/v1/chat/messages/${parentId}/replies?**`, route => {
    if (fails) return route.fulfill({ status: 403, json: { detail: "Replies denied" } });
    const before = new URL(route.request().url()).searchParams.get("before");
    if (before) expect(before).toBe(many[19].id);
    return route.fulfill({ json: before ? [] : many.map(row => ({ ...row, parentMessageId: parentId })) });
  });
  await page.goto(`/chat/${channelId}`);
  const list = page.getByRole("region", { name: en.chat.messages, exact: true });
  await list.getByRole("button", { name: en.chat.older }).click();
  await expect(list.getByText(en.chat.noMessages)).toBeVisible();
  await list.getByRole("button", { name: en.chat.newer }).click();
  await list.getByRole("link", { name: /View thread/ }).first().click();
  const replies = page.getByRole("region", { name: en.chat.replies, exact: true });
  await expect(replies.getByText("Replies denied")).toBeVisible();
  fails = false;
  await replies.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(replies.getByText("Message 0", { exact: true })).toBeVisible();
  await replies.getByRole("button", { name: en.chat.older }).click();
  await expect(replies.getByText(en.chat.noMessages)).toBeVisible();
  await expect(list.getByText("Message 0", { exact: true })).toBeVisible();
});

test("invalid identifiers do not produce corresponding requests", async ({ page }) => {
  const requests = await setup(page);
  await page.goto("/chat/not-a-guid?messageId=bad");
  await expect(page.getByText(en.chat.invalidId)).toBeVisible();
  expect(requests).toEqual([]);
  await page.goto(`/chat/${channelId}?messageId=bad`);
  await expect(page.getByText(en.chat.invalidId)).toBeVisible();
  await expect(page.getByText("Latest page message")).toBeVisible();
  expect(requests.some(path => path.endsWith("/bad") || path.endsWith("/replies"))).toBe(false);
});

test("message send permission alone cannot open chat navigation or direct URL", async ({ page }) => {
  const requests = await setup(page, ["Permissions.Chat.Messages.Send"]);
  await page.goto(`/chat/${channelId}?messageId=${replyId}`);
  await expect(page.getByRole("heading", { name: en.common.forbiddenTitle })).toBeVisible();
  await expect(page.locator('a[href="/chat"]')).toHaveCount(0);
  expect(requests).toEqual([]);
});

test("loading protects dependent reads and message text does not execute markup or load attachment URLs", async ({ page }) => {
  const requests = await setup(page);
  let release!: () => void;
  const hold = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/v1/chat/channels/${channelId}`, async route => { await hold; return route.fulfill({ json: channel }); });
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [{ ...message, body: '<img src="https://untrusted.invalid/image" onerror="alert(1)">', attachments: [{ id: "attachment", url: "https://untrusted.invalid/file", originalFileName: "invoice.pdf" }] }] }));
  await page.goto(`/chat/${channelId}`);
  await expect(page.getByRole("main").getByText(en.common.loading)).toBeVisible();
  expect(requests.some(path => path.includes("/messages"))).toBe(false);
  const external: string[] = [];
  page.on("request", request => { if (request.url().includes("untrusted.invalid")) external.push(request.url()); });
  release();
  await expect(page.getByText("invoice.pdf")).toBeVisible();
  await expect(page.getByText(en.chat.attachmentHint)).toBeVisible();
  await expect(page.locator('img[src*="untrusted.invalid"],a[href*="untrusted.invalid"]')).toHaveCount(0);
  expect(external).toEqual([]);
});
