import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const messageId = "44444444-4444-4444-4444-444444444444";
const view = "Permissions.Chat.Channels.View";
const send = "Permissions.Chat.Messages.Send";
const channel = {
  id: channelId,
  type: "Channel",
  name: "Operator team",
  isPrivate: true,
  unreadCount: 0,
  members: [{ userId: TEST_USER.sub }],
};

function message(overrides: Record<string, unknown> = {}) {
  return {
    id: messageId,
    channelId,
    body: "Release checklist",
    parentMessageId: null,
    authorUserId: TEST_USER.sub,
    replyCount: 0,
    createdAtUtc: "2026-09-24T00:00:00Z",
    attachments: [],
    reactions: [],
    isPinned: false,
    ...overrides,
  };
}

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
}

test("member searches, pins and toggles their own reaction without losing channel scope", async ({ page }) => {
  await setup(page, [view, send]);
  let pinned = false;
  let reacted = false;
  const current = () => message({
    isPinned: pinned,
    reactions: reacted ? [{ id: "reaction-1", userId: TEST_USER.sub, emoji: "👍" }] : [],
  });

  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [current()] }));
  await page.route("**/api/v1/chat/search?**", route => {
    const url = new URL(route.request().url());
    expect(url.searchParams.get("q")).toBe("release");
    expect(url.searchParams.get("channelId")).toBe(channelId);
    return route.fulfill({ json: [current()] });
  });
  await page.route(`**/api/v1/chat/channels/${channelId}/pinned`, route => route.fulfill({ json: pinned ? [current()] : [] }));
  await page.route(`**/api/v1/chat/messages/${messageId}/pin`, route => {
    expect(route.request().headers().tenant).toBe("root");
    if (route.request().method() === "POST") pinned = true;
    else if (route.request().method() === "DELETE") pinned = false;
    else return route.fulfill({ status: 405 });
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/v1/chat/messages/${messageId}/reactions`, route => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().postDataJSON()).toEqual({ emoji: "👍" });
    reacted = true;
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/v1/chat/messages/${messageId}/reactions/%F0%9F%91%8D`, route => {
    expect(route.request().method()).toBe("DELETE");
    reacted = false;
    return route.fulfill({ status: 204 });
  });

  await page.goto(`/chat/${channelId}`);
  const discovery = page.getByRole("region", { name: en.chatDiscovery.title });
  await discovery.getByLabel(en.chatDiscovery.searchLabel).fill(" release ");
  await discovery.getByRole("button", { name: en.chatDiscovery.searchAction }).click();
  await expect(discovery.getByRole("region", { name: en.chatDiscovery.searchResults }).getByText("Release checklist")).toBeVisible();

  await discovery.getByRole("button", { name: en.chatDiscovery.showPinned }).click();
  const pinnedRegion = discovery.getByRole("region", { name: en.chatDiscovery.pinnedMessages });
  await expect(pinnedRegion.getByText(en.chatDiscovery.noPinned)).toBeVisible();

  const messages = page.getByRole("region", { name: en.chat.messages });
  await messages.getByRole("button", { name: en.chatDiscovery.pin }).click();
  await expect(messages.getByRole("button", { name: en.chatDiscovery.unpin })).toBeVisible();
  await expect(pinnedRegion.getByText("Release checklist")).toBeVisible();

  await messages.getByRole("button", { name: `${en.chatDiscovery.addReaction} 👍` }).click();
  await expect(messages.getByRole("button", { name: `${en.chatDiscovery.removeReaction} 👍` })).toHaveAttribute("aria-pressed", "true");
  await messages.getByRole("button", { name: `${en.chatDiscovery.removeReaction} 👍` }).click();
  await expect(messages.getByRole("button", { name: `${en.chatDiscovery.addReaction} 👍` })).toHaveAttribute("aria-pressed", "false");

  await messages.getByRole("button", { name: en.chatDiscovery.unpin }).click();
  await expect(messages.getByRole("button", { name: en.chatDiscovery.pin })).toBeVisible();
  await expect(pinnedRegion.getByText(en.chatDiscovery.noPinned)).toBeVisible();
});

test("read-only member can search and inspect pins but cannot mutate messages", async ({ page }) => {
  await setup(page, [view]);
  const row = message({ isPinned: true, reactions: [{ id: "r", userId: "other", emoji: "🎉" }] });
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [row] }));
  await page.route("**/api/v1/chat/search?**", route => route.fulfill({ json: [row] }));
  await page.route(`**/api/v1/chat/channels/${channelId}/pinned`, route => route.fulfill({ json: [row] }));

  await page.goto(`/chat/${channelId}`);
  const discovery = page.getByRole("region", { name: en.chatDiscovery.title });
  await discovery.getByLabel(en.chatDiscovery.searchLabel).fill("release");
  await discovery.getByRole("button", { name: en.chatDiscovery.searchAction }).click();
  await discovery.getByRole("button", { name: en.chatDiscovery.showPinned }).click();
  await expect(discovery.getByText("Release checklist").first()).toBeVisible();
  await expect(page.getByRole("button", { name: en.chatDiscovery.unpin })).toHaveCount(0);
  await expect(page.getByRole("button", { name: new RegExp(`^${en.chatDiscovery.addReaction}`) })).toHaveCount(0);
});

test("search and pinned failures recover independently without issuing writes", async ({ page }) => {
  await setup(page, [view]);
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [] }));
  let searchFails = true;
  let pinnedFails = true;
  await page.route("**/api/v1/chat/search?**", route => route.fulfill(searchFails
    ? { status: 403, json: { detail: "Search denied" } }
    : { json: [] }));
  await page.route(`**/api/v1/chat/channels/${channelId}/pinned`, route => route.fulfill(pinnedFails
    ? { status: 404, json: { detail: "Pins unavailable" } }
    : { json: [] }));

  await page.goto(`/chat/${channelId}`);
  const discovery = page.getByRole("region", { name: en.chatDiscovery.title });
  await discovery.getByLabel(en.chatDiscovery.searchLabel).fill("release");
  await discovery.getByRole("button", { name: en.chatDiscovery.searchAction }).click();
  await discovery.getByRole("button", { name: en.chatDiscovery.showPinned }).click();

  const results = discovery.getByRole("region", { name: en.chatDiscovery.searchResults });
  const pins = discovery.getByRole("region", { name: en.chatDiscovery.pinnedMessages });
  await expect(results.getByText("Search denied")).toBeVisible();
  await expect(pins.getByText("Pins unavailable")).toBeVisible();

  searchFails = false;
  await results.getByRole("button", { name: en.workbench.retry }).click();
  await expect(results.getByText(en.chatDiscovery.noSearchResults)).toBeVisible();
  await expect(pins.getByText("Pins unavailable")).toBeVisible();

  pinnedFails = false;
  await pins.getByRole("button", { name: en.workbench.retry }).click();
  await expect(pins.getByText(en.chatDiscovery.noPinned)).toBeVisible();
});

test("chat view permission opens realtime without requiring notification permission", async ({ page }) => {
  await setup(page, [view]);
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [] }));
  let realtimeAttempts = 0;
  await page.route("**/api/v1/realtime/**", route => {
    realtimeAttempts += 1;
    return route.abort();
  });

  await page.goto(`/chat/${channelId}`);
  await expect.poll(() => realtimeAttempts).toBeGreaterThan(0);
});
