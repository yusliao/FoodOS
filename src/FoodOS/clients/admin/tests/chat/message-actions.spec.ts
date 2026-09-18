import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333", ownId = "44444444-4444-4444-4444-444444444444", otherId = "55555555-5555-5555-5555-555555555555";
const view = "Permissions.Chat.Channels.View", edit = "Permissions.Chat.Messages.EditOwn", remove = "Permissions.Chat.Messages.DeleteOwn", moderate = "Permissions.Chat.Messages.DeleteAny";
const message = { id: ownId, channelId, body: "My original", authorUserId: TEST_USER.sub, parentMessageId: null, replyCount: 0, createdAtUtc: "2026-09-17T00:00:00Z", deletedAtUtc: null as string | null, attachments: [], reactions: [] };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: { id: channelId, name: "Operator team", members: [{ userId: TEST_USER.sub }] } }));
  const state = { rows: [message, { ...message, id: otherId, authorUserId: "another-user", body: "Their original" }] };
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: state.rows }));
  return state;
}
for (const [name, permissions, ownEdit, ownDelete, otherDelete] of [
  ["readonly", [view], false, false, false],
  ["editor", [view, edit], true, false, false],
  ["own delete", [view, remove], false, true, false],
  ["DeleteAny alone", [view, moderate], false, false, false],
  ["moderator", [view, edit, remove, moderate], true, true, true],
] as const) test(`${name} message actions honor author and exact permission combination`, async ({ page }) => {
  await setup(page, [...permissions]);
  const writes: string[] = [];
  page.on("request", request => { if (request.url().includes("/chat/") && request.method() !== "GET") writes.push(request.url()); });
  await page.goto(`/chat/${channelId}`);
  const own = page.getByRole("article").filter({ hasText: "My original" });
  const other = page.getByRole("article").filter({ hasText: "Their original" });
  await expect(own).toBeVisible();
  await expect(own.getByRole("button", { name: en.chatActions.edit })).toHaveCount(ownEdit ? 1 : 0);
  await expect(own.getByRole("button", { name: en.chatActions.delete })).toHaveCount(ownDelete ? 1 : 0);
  await expect(other.getByRole("button", { name: en.chatActions.edit })).toHaveCount(0);
  await expect(other.getByRole("button", { name: en.chatActions.delete })).toHaveCount(otherDelete ? 1 : 0);
  expect(writes).toEqual([]);
});

for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} edit preserves failed input, cancels cleanly and waits for refresh`, async ({ page }) => {
    const state = await setup(page, [view, edit, remove]);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let failed = true, saved = false, release!: () => void;
    const hold = new Promise<void>(resolve => { release = resolve; });
    const bodies: unknown[] = [];
    await page.route(`**/api/v1/chat/messages/${ownId}`, route => {
      expect(route.request().method()).toBe("PUT");
      expect(route.request().headers().tenant).toBe("root");
      bodies.push(route.request().postDataJSON());
      if (failed) return route.fulfill({ status: 403, json: { detail: "Edit denied" } });
      state.rows[0] = { ...message, body: route.request().postDataJSON().body };
      saved = true;
      return route.fulfill({ status: 204 });
    });
    await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, async route => { if (saved) await hold; return route.fulfill({ json: state.rows }); });
    await page.goto(`/chat/${channelId}`);
    const own = page.getByRole("article").filter({ hasText: "My original" });
    await own.getByRole("button", { name: m.chatActions.edit }).click();
    const dialog = page.getByRole("dialog", { name: m.chatActions.edit });
    await dialog.getByRole("textbox").fill(" Edited text ");
    await dialog.getByRole("button", { name: m.chatActions.save }).click();
    await expect(dialog.getByText("Edit denied")).toBeVisible();
    await expect(dialog.getByRole("textbox")).toHaveValue(" Edited text ");
    await dialog.getByRole("button", { name: m.chrome.cancel }).click();
    await own.getByRole("button", { name: m.chatActions.edit }).click();
    await expect(dialog.getByText("Edit denied")).toHaveCount(0);
    await expect(dialog.getByRole("textbox")).toHaveValue("My original");
    failed = false;
    await dialog.getByRole("textbox").fill(" Edited text ");
    await dialog.getByRole("button", { name: m.chatActions.save }).click();
    await expect.poll(() => saved).toBe(true);
    await expect(dialog.getByRole("textbox")).toBeDisabled();
    await expect(dialog.getByRole("button", { name: m.chrome.cancel })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    release();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText("Edited text", { exact: true })).toBeVisible();
    expect(bodies).toEqual([{ body: "Edited text" }, { body: "Edited text" }]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });

  test(`${culture} moderator deletion confirms, clears cancelled errors and preserves tombstone`, async ({ page }) => {
    const state = await setup(page, [view, remove, moderate]);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    let failed = true, saved = false, writes = 0, release!: () => void;
    const hold = new Promise<void>(resolve => { release = resolve; });
    await page.route(`**/api/v1/chat/messages/${otherId}`, route => {
      expect(route.request().method()).toBe("DELETE");
      expect(route.request().headers().tenant).toBe("root");
      writes++;
      if (failed) return route.fulfill({ status: 403, json: { detail: "Delete denied" } });
      state.rows[1] = { ...state.rows[1], body: "", deletedAtUtc: "2026-09-17T01:00:00Z" };
      saved = true;
      return route.fulfill({ status: 204 });
    });
    await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, async route => { if (saved) await hold; return route.fulfill({ json: state.rows }); });
    await page.goto(`/chat/${channelId}`);
    const other = page.getByRole("article").filter({ hasText: "Their original" });
    await other.getByRole("button", { name: m.chatActions.delete }).click();
    const dialog = page.getByRole("dialog", { name: m.chatActions.delete });
    expect(writes).toBe(0);
    await dialog.getByRole("button", { name: m.chatActions.delete }).click();
    await expect(dialog.getByText("Delete denied")).toBeVisible();
    await dialog.getByRole("button", { name: m.chrome.cancel }).click();
    await other.getByRole("button", { name: m.chatActions.delete }).click();
    await expect(dialog.getByText("Delete denied")).toHaveCount(0);
    expect(writes).toBe(1);
    failed = false;
    await dialog.getByRole("button", { name: m.chatActions.delete }).click();
    await expect.poll(() => saved).toBe(true);
    await expect(dialog.getByRole("button", { name: m.chrome.cancel })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    release();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText(m.chat.deleted)).toBeVisible();
    const tombstone = page.getByRole("article").filter({ hasText: m.chat.deleted });
    await expect(tombstone.getByRole("button", { name: m.chatActions.edit })).toHaveCount(0);
    await expect(tombstone.getByRole("button", { name: m.chatActions.delete })).toHaveCount(0);
    await expect(tombstone.getByRole("button", { name: m.chatRead.action })).toBeVisible();
    expect(writes).toBe(2);
  });
}

test("deleted parent opens retained replies without requesting deleted message or attachments", async ({ page }) => {
  const state = await setup(page, [view, edit, remove]);
  state.rows[0] = { ...message, body: "", deletedAtUtc: "2026-09-17T01:00:00Z", replyCount: 1 };
  const requests: string[] = [];
  page.on("request", request => { if (request.url().includes("/chat/")) requests.push(new URL(request.url()).pathname); });
  await page.route(`**/api/v1/chat/messages/${ownId}/replies?**`, route => route.fulfill({ json: [{ ...message, id: otherId, body: "Retained reply", parentMessageId: ownId }] }));
  await page.goto(`/chat/${channelId}`);
  const tombstone = page.getByRole("article").filter({ hasText: en.chat.deleted });
  await tombstone.getByRole("button", { name: "View thread (1)" }).click();
  await expect(tombstone.getByText("Retained reply")).toBeVisible();
  expect(requests).not.toContain(`/api/v1/chat/channels/${channelId}/messages/${ownId}`);
  await tombstone.getByRole("button", { name: "View thread (1)" }).click();
  await expect(page.getByText("Retained reply")).toHaveCount(0);
});

test("deleted-state conflict retains edit text without automatic repeat writes", async ({ page }) => {
  await setup(page, [view, edit]);
  let writes = 0;
  await page.route(`**/api/v1/chat/messages/${ownId}`, route => { writes++; return route.fulfill({ status: 409, json: { detail: "Message was deleted" } }); });
  await page.goto(`/chat/${channelId}`);
  await page.getByRole("button", { name: en.chatActions.edit }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox").fill("Keep my draft");
  await dialog.getByRole("button", { name: en.chatActions.save }).click();
  await expect(dialog.getByText("Message was deleted")).toBeVisible();
  await expect(dialog.getByRole("textbox")).toHaveValue("Keep my draft");
  await dialog.getByRole("button", { name: en.chrome.cancel }).click();
  expect(writes).toBe(1);
});
