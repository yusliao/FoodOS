import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const olderId = "44444444-4444-4444-4444-444444444444";
const newerId = "55555555-5555-5555-5555-555555555555";
const view = "Permissions.Chat.Channels.View";
const baseMessage = { id: newerId, channelId, authorUserId: "operator-author", body: "Newest message", parentMessageId: null, replyCount: 0, createdAtUtc: "2026-09-18T01:00:00Z", deletedAtUtc: null, attachments: [], reactions: [] };

async function setup(page: Page, permissions = [view]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const channel = { id: channelId, type: "Channel", name: "Operator team", isPrivate: true, unreadCount: 2, description: "Read markers", members: [{ id: "membership", userId: TEST_USER.sub, role: "Member", lastReadMessageId: null }] };
  const requests: { method: string; path: string }[] = [];
  page.on("request", request => {
    if (request.url().includes("/api/v1/chat/")) {
      expect(request.headers().tenant).toBe("root");
      requests.push({ method: request.method(), path: new URL(request.url()).pathname });
    }
  });
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [baseMessage, { ...baseMessage, id: olderId, body: "Older message", createdAtUtc: "2026-09-18T00:00:00Z" }] }));
  return { channel, requests };
}

for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} view-only member explicitly advances read marker and waits for refreshed unread count`, async ({ page }) => {
    const { channel, requests } = await setup(page);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let saved = false, release!: () => void;
    const hold = new Promise<void>(resolve => { release = resolve; });
    await page.route(`**/api/v1/chat/channels/${channelId}/read`, route => {
      expect(route.request().method()).toBe("POST");
      expect(route.request().postDataJSON()).toEqual({ messageId: newerId });
      channel.unreadCount = 0;
      channel.members[0].lastReadMessageId = newerId;
      saved = true;
      return route.fulfill({ status: 204 });
    });
    await page.route(`**/api/v1/chat/channels/${channelId}`, async route => {
      if (saved) await hold;
      return route.fulfill({ json: channel });
    });
    await page.goto(`/chat/${channelId}`);
    expect(requests.filter(request => request.method === "POST")).toEqual([]);
    const newest = page.getByRole("article").filter({ hasText: "Newest message" });
    await newest.getByRole("button", { name: m.chatRead.action }).click();
    await expect.poll(() => saved).toBe(true);
    await expect(newest.getByRole("button", { name: m.common.working })).toBeDisabled();
    await expect(newest.getByText(m.chatRead.success)).toHaveCount(0);
    release();
    await expect(newest.getByText(m.chatRead.success)).toBeVisible();
    await expect(page.getByText(`${m.chat.unread}: 0`, { exact: true })).toBeVisible();
    expect(requests.filter(request => request.method === "POST")).toHaveLength(1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("failed read marker stays explicit and retries the same message without automatic writes", async ({ page }) => {
  await setup(page);
  let writes = 0;
  await page.route(`**/api/v1/chat/channels/${channelId}/read`, route => {
    writes++;
    expect(route.request().postDataJSON()).toEqual({ messageId: olderId });
    return route.fulfill(writes === 1 ? { status: 403, json: { detail: "Read marker denied" } } : { status: 204 });
  });
  await page.goto(`/chat/${channelId}`);
  const older = page.getByRole("article").filter({ hasText: "Older message" });
  await expect(older.getByRole("button", { name: en.chatRead.action })).toBeVisible();
  expect(writes).toBe(0);
  await older.getByRole("button", { name: en.chatRead.action }).click();
  await expect(older.getByText("Read marker denied")).toBeVisible();
  expect(writes).toBe(1);
  await page.waitForTimeout(100);
  expect(writes).toBe(1);
  await older.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(older.getByText(en.chatRead.success)).toBeVisible();
  expect(writes).toBe(2);
  await expect(page.getByText(en.notifications.markAll, { exact: true })).toHaveCount(0);
});

test("missing channel view permission blocks direct URL and all chat requests", async ({ page }) => {
  const { requests } = await setup(page, []);
  await page.goto(`/chat/${channelId}`);
  await expect(page.getByRole("heading", { name: en.common.forbiddenTitle })).toBeVisible();
  expect(requests).toEqual([]);
});
