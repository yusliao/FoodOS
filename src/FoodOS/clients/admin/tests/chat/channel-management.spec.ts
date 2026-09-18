import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const channelId = "33333333-3333-3333-3333-333333333333";
const newChannelId = "66666666-6666-6666-6666-666666666666";
const otherId = "77777777-7777-7777-7777-777777777777";
const candidateId = "88888888-8888-8888-8888-888888888888";
const view = "Permissions.Chat.Channels.View";
const create = "Permissions.Chat.Channels.Create";
const users = "Permissions.Users.View";
type SetupOptions = { role?: "Member" | "Admin"; private?: boolean; type?: "Channel" | "DirectMessage" | "GroupMessage" };

async function setup(page: Page, permissions: string[], options: SetupOptions = {}) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const channel = {
    id: channelId, type: options.type ?? "Channel", name: "Operator team", isPrivate: options.private ?? true,
    unreadCount: 0, description: "Managed channel", members: [
      { id: "membership-me", userId: TEST_USER.sub, role: options.role ?? "Admin", lastReadMessageId: null },
      { id: "membership-other", userId: otherId, role: "Member", lastReadMessageId: null },
    ],
  };
  const requests: { method: string; path: string }[] = [];
  page.on("request", request => {
    if (request.url().includes("/api/v1/chat/") || request.url().includes("/api/v1/identity/users/search")) {
      expect(request.headers().tenant).toBe("root");
      requests.push({ method: request.method(), path: new URL(request.url()).pathname });
    }
  });
  await page.route("**/api/v1/chat/**", route => route.fulfill({ status: 500, json: { detail: "Unexpected chat request" } }));
  await page.route("**/api/v1/chat/channels?**", route => route.fulfill({ json: [channel] }));
  await page.route(`**/api/v1/chat/channels/${channelId}`, route => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/chat/channels/${channelId}/messages?**`, route => route.fulfill({ json: [] }));
  return { channel, requests };
}

for (const [name, permissions, options, createVisible, editVisible, addVisible, removeOther] of [
  ["view-only private admin without user lookup", [view], { role: "Admin", private: true }, false, false, false, true],
  ["creator who is only a private member", [view, create, users], { role: "Member", private: true }, true, false, false, false],
  ["public member with user lookup", [view, users], { role: "Member", private: false }, false, false, true, false],
  ["full channel administrator", [view, create, users], { role: "Admin", private: true }, true, true, true, true],
] as const) test(`${name} sees only server-supported channel actions`, async ({ page }) => {
  const { requests } = await setup(page, [...permissions], options);
  await page.goto(`/chat/${channelId}`);
  const members = page.getByRole("region", { name: en.chatManage.members });
  await expect(members).toBeVisible();
  await expect(members.getByRole("button", { name: en.chatManage.edit })).toHaveCount(editVisible ? 1 : 0);
  await expect(members.getByRole("button", { name: en.chatManage.add })).toHaveCount(addVisible ? 1 : 0);
  await expect(members.getByRole("button", { name: en.chatManage.archive })).toHaveCount(editVisible ? 1 : 0);
  await expect(members.getByRole("button", { name: en.chatManage.remove })).toHaveCount(removeOther ? 1 : 0);
  await expect(members.getByRole("button", { name: en.chatManage.leave })).toBeVisible();
  await page.getByRole("link", { name: en.chat.back }).click();
  await expect(page.getByRole("button", { name: en.chatManage.create })).toHaveCount(createVisible ? 1 : 0);
  expect(requests.filter(request => request.path.includes("/identity/users"))).toEqual([]);
});

test("direct message membership is fixed and has no channel administration actions", async ({ page }) => {
  await setup(page, [view, create, users], { role: "Admin", private: true, type: "DirectMessage" });
  await page.goto(`/chat/${channelId}`);
  const members = page.getByRole("region", { name: en.chatManage.members });
  await expect(members.getByRole("button", { name: en.chatManage.add })).toHaveCount(0);
  await expect(members.getByRole("button", { name: en.chatManage.remove })).toHaveCount(0);
  await expect(members.getByRole("button", { name: en.chatManage.leave })).toHaveCount(0);
  await expect(members.getByRole("button", { name: en.chatManage.edit })).toHaveCount(0);
  await expect(members.getByRole("button", { name: en.chatManage.archive })).toHaveCount(0);
});

for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} create channel preserves 403 input, resets on cancel and waits for list refresh`, async ({ page }) => {
    await setup(page, [view, create]);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let failed = true, saved = false, release!: () => void;
    const hold = new Promise<void>(resolve => { release = resolve; });
    const bodies: unknown[] = [];
    await page.route("**/api/v1/chat/channels", route => {
      expect(route.request().method()).toBe("POST");
      bodies.push(route.request().postDataJSON());
      if (failed) return route.fulfill({ status: 403, json: { detail: "Create denied" } });
      saved = true;
      return route.fulfill({ json: newChannelId });
    });
    await page.route("**/api/v1/chat/channels?**", async route => { if (saved) await hold; return route.fulfill({ json: [] }); });
    await page.route(`**/api/v1/chat/channels/${newChannelId}`, route => route.fulfill({ json: { id: newChannelId, type: "Channel", name: "Night shift", isPrivate: true, unreadCount: 0, members: [{ id: "new-member", userId: TEST_USER.sub, role: "Admin" }] } }));
    await page.route(`**/api/v1/chat/channels/${newChannelId}/messages?**`, route => route.fulfill({ json: [] }));
    await page.goto("/chat");
    await page.getByRole("button", { name: m.chatManage.create }).click();
    let dialog = page.getByRole("dialog", { name: m.chatManage.create });
    await dialog.getByLabel(m.chatManage.name).fill(" Night shift ");
    await dialog.getByLabel(m.chatManage.description).fill(" Overnight operations ");
    await dialog.getByLabel(m.chatManage.private).check();
    await dialog.getByRole("button", { name: m.chatManage.save }).click();
    await expect(dialog.getByText("Create denied")).toBeVisible();
    await expect(dialog.getByLabel(m.chatManage.name)).toHaveValue(" Night shift ");
    await dialog.getByRole("button", { name: m.chrome.cancel }).click();
    await page.getByRole("button", { name: m.chatManage.create }).click();
    dialog = page.getByRole("dialog", { name: m.chatManage.create });
    await expect(dialog.getByText("Create denied")).toHaveCount(0);
    await expect(dialog.getByLabel(m.chatManage.name)).toHaveValue("");
    failed = false;
    await dialog.getByLabel(m.chatManage.name).fill("Night shift");
    await dialog.getByLabel(m.chatManage.private).check();
    await dialog.getByRole("button", { name: m.chatManage.save }).click();
    await expect.poll(() => saved).toBe(true);
    await expect(dialog.getByLabel(m.chatManage.name)).toBeDisabled();
    release();
    await expect(page).toHaveURL(`/chat/${newChannelId}`);
    expect(bodies).toEqual([
      { name: "Night shift", description: "Overnight operations", isPrivate: true },
      { name: "Night shift", description: null, isPrivate: true },
    ]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("channel admin edits and archives only after refresh, retaining failed edit", async ({ page }) => {
  const { channel } = await setup(page, [view, create, users]);
  let editFails = true, edited = false, archived = false, releaseEdit!: () => void, releaseArchive!: () => void;
  const editHold = new Promise<void>(resolve => { releaseEdit = resolve; });
  const archiveHold = new Promise<void>(resolve => { releaseArchive = resolve; });
  await page.route(`**/api/v1/chat/channels/${channelId}`, async route => {
    const method = route.request().method();
    if (method === "PUT") {
      if (editFails) return route.fulfill({ status: 403, json: { detail: "Edit denied" } });
      channel.name = route.request().postDataJSON().name;
      edited = true;
      return route.fulfill({ status: 204 });
    }
    if (method === "DELETE") { archived = true; return route.fulfill({ status: 204 }); }
    if (edited) await editHold;
    return route.fulfill({ json: channel });
  });
  await page.route("**/api/v1/chat/channels?**", async route => { if (archived) await archiveHold; return route.fulfill({ json: archived ? [] : [channel] }); });
  await page.goto(`/chat/${channelId}`);
  const members = page.getByRole("region", { name: en.chatManage.members });
  await members.getByRole("button", { name: en.chatManage.edit }).click();
  let dialog = page.getByRole("dialog", { name: en.chatManage.edit });
  await dialog.getByLabel(en.chatManage.name).fill(" Renamed team ");
  await dialog.getByRole("button", { name: en.chatManage.save }).click();
  await expect(dialog.getByText("Edit denied")).toBeVisible();
  await expect(dialog.getByLabel(en.chatManage.name)).toHaveValue(" Renamed team ");
  editFails = false;
  await dialog.getByRole("button", { name: en.chatManage.save }).click();
  await expect.poll(() => edited).toBe(true);
  await expect(dialog.getByLabel(en.chatManage.name)).toBeDisabled();
  releaseEdit();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Renamed team" })).toBeVisible();
  await members.getByRole("button", { name: en.chatManage.archive }).click();
  dialog = page.getByRole("dialog", { name: en.chatManage.archive });
  await dialog.getByRole("button", { name: en.chatManage.archive }).click();
  await expect.poll(() => archived).toBe(true);
  await expect(dialog.getByRole("button", { name: en.chrome.cancel })).toBeDisabled();
  releaseArchive();
  await expect(page).toHaveURL("/chat");
});

test("member lookup recovers, filters existing users and add waits for channel refresh", async ({ page }) => {
  const { channel } = await setup(page, [view, users], { role: "Member", private: false });
  let lookupFails = true, saved = false, release!: () => void;
  const hold = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/identity/users/search?**", route => route.fulfill(lookupFails ? { status: 403, json: { detail: "Lookup denied" } } : { json: { items: [{ id: TEST_USER.sub, userName: "self", isActive: true }, { id: candidateId, userName: "candidate", isActive: true }], pageNumber: 1, pageSize: 10, totalCount: 2, totalPages: 1, hasPrevious: false, hasNext: false } }));
  await page.route(`**/api/v1/chat/channels/${channelId}/members`, route => {
    expect(route.request().method()).toBe("POST");
    expect(route.request().postDataJSON()).toEqual({ channelId, userIds: [candidateId] });
    channel.members.push({ id: "membership-candidate", userId: candidateId, role: "Member", lastReadMessageId: null });
    saved = true;
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/v1/chat/channels/${channelId}`, async route => { if (saved) await hold; return route.fulfill({ json: channel }); });
  await page.goto(`/chat/${channelId}`);
  await page.getByRole("region", { name: en.chatManage.members }).getByRole("button", { name: en.chatManage.add }).click();
  const dialog = page.getByRole("dialog", { name: en.chatManage.add });
  await expect(dialog.getByText("Lookup denied")).toBeVisible();
  lookupFails = false;
  await dialog.getByRole("button", { name: en.workbench.retry, exact: true }).click();
  await expect(dialog.getByRole("button", { name: new RegExp(`candidate.*${candidateId}`) })).toBeVisible();
  await expect(dialog.getByText("self", { exact: false })).toHaveCount(0);
  await dialog.getByRole("button", { name: new RegExp(`candidate.*${candidateId}`) }).click();
  await dialog.getByRole("button", { name: en.chatManage.add, exact: true }).click();
  await expect.poll(() => saved).toBe(true);
  await expect(dialog.getByLabel(en.chatManage.search)).toBeDisabled();
  release();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByText(candidateId, { exact: false })).toBeVisible();
});

test("admin remove failure is explicit and self leave navigates only after refresh", async ({ page }) => {
  const { channel } = await setup(page, [view, users]);
  let removeFails = true, leaving = false, release!: () => void;
  const hold = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/v1/chat/channels/${channelId}/members/${otherId}`, route => {
    if (removeFails) return route.fulfill({ status: 403, json: { detail: "Remove denied" } });
    channel.members = channel.members.filter(member => member.userId !== otherId);
    return route.fulfill({ status: 204 });
  });
  await page.route(`**/api/v1/chat/channels/${channelId}/members/${TEST_USER.sub}`, route => { leaving = true; return route.fulfill({ status: 204 }); });
  await page.route("**/api/v1/chat/channels?**", async route => { if (leaving) await hold; return route.fulfill({ json: leaving ? [] : [channel] }); });
  await page.goto(`/chat/${channelId}`);
  const members = page.getByRole("region", { name: en.chatManage.members });
  await members.getByRole("button", { name: en.chatManage.remove }).click();
  let dialog = page.getByRole("dialog", { name: en.chatManage.remove });
  await dialog.getByRole("button", { name: en.chatManage.remove }).click();
  await expect(dialog.getByText("Remove denied")).toBeVisible();
  await dialog.getByRole("button", { name: en.chrome.cancel }).click();
  removeFails = false;
  await members.getByRole("button", { name: en.chatManage.remove }).click();
  dialog = page.getByRole("dialog", { name: en.chatManage.remove });
  await dialog.getByRole("button", { name: en.chatManage.remove }).click();
  await expect(page.getByText(otherId, { exact: false })).toHaveCount(0);
  await members.getByRole("button", { name: en.chatManage.leave }).click();
  dialog = page.getByRole("dialog", { name: en.chatManage.leave });
  await dialog.getByRole("button", { name: en.chatManage.leave }).click();
  await expect.poll(() => leaving).toBe(true);
  await expect(dialog.getByRole("button", { name: en.chrome.cancel })).toBeDisabled();
  release();
  await expect(page).toHaveURL("/chat");
});
