import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };
const id = "33333333-3333-3333-3333-333333333333";
const ticket = { id, number: "TK-1", title: "Old ticket", status: "Open", priority: "Low", customerTenantId: "acme", reporterUserId: "reporter", createdAtUtc: "2026-09-17T00:00:00Z", commentCount: 0 };
for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const mode of ["delete", "restore"] as const) test(`${culture} ticket ${mode} confirms, retries and refreshes with root identity`, async ({ page }) => {
    const permissions = mode === "delete" ? ["Permissions.Tickets.View", "Permissions.Tickets.Delete"] : ["Permissions.Tickets.Restore"];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let saved = false;
    await page.route("**/api/v1/tickets?**", route => route.fulfill({ json: paged(saved ? [] : [ticket]) }));
    await page.route("**/api/v1/tickets/trash?**", route => route.fulfill({ json: paged(saved ? [] : [ticket]) }));
    await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
    await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
    const writes: string[] = [];
    let release!: () => void;
    const held = new Promise<void>(resolve => { release = resolve; });
    await page.route(mode === "delete" ? `**/api/v1/tickets/${id}` : `**/api/v1/tickets/${id}/restore`, async route => {
      if (route.request().method() === "GET") return route.fallback();
      expect(route.request().method()).toBe(mode === "delete" ? "DELETE" : "POST");
      expect(route.request().headers().tenant).toBe("root");
      writes.push(route.request().headers()["idempotency-key"] ?? "");
      if (writes.length === 1) return route.fulfill({ status: 403, json: { detail: "Operation denied" } });
      await held;
      saved = true;
      return route.fulfill(mode === "delete" ? { status: 204 } : { json: id });
    });
    await page.goto(mode === "delete" ? `/tickets/${id}` : "/tickets/trash");
    const normalReads: string[] = [];
    page.on("request", request => { if (/\/tickets\?/.test(request.url())) normalReads.push(request.url()); });
    await page.getByRole("button", { name: m.tickets[mode], exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: m.tickets[mode], exact: true }).click();
    await expect(dialog.getByRole("alert")).toHaveText("Operation denied");
    await dialog.getByRole("button", { name: m.tickets[mode], exact: true }).click();
    await expect(dialog.getByRole("button", { name: m.common.working, exact: true })).toBeDisabled();
    await expect(dialog.getByRole("button", { name: m.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    release();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText(mode === "delete" ? m.tickets.empty : m.tickets.trashEmpty)).toBeVisible();
    if (mode === "restore") { expect(writes[0]).toBeTruthy(); expect(writes[1]).toBe(writes[0]); expect(normalReads).toEqual([]); }
    expect(writes).toHaveLength(2);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
test("ticket trash retries failed page and reaches later deleted records", async ({ page }) => {
  const permissions = ["Permissions.Tickets.Restore"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  let fail = true;
  await page.route("**/api/v1/tickets/trash?**", route => {
    const pageNumber = Number(new URL(route.request().url()).searchParams.get("pageNumber"));
    return route.fulfill(fail ? { status: 403, json: { detail: "Trash unavailable" } } : { json: paged([{ ...ticket, title: `Deleted page ${pageNumber}` }], { pageNumber, totalPages: 2, totalCount: 21 }) });
  });
  await page.goto("/tickets/trash");
  await expect(page.getByText("Trash unavailable")).toBeVisible();
  await expect(page.getByText(en.tickets.trashEmpty)).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Deleted page 1" })).toBeVisible();
  await page.getByRole("button", { name: /Next/ }).click();
  await expect(page.getByRole("heading", { name: "Deleted page 2" })).toBeVisible();
});
test("Delete does not grant Restore route or navigation", async ({ page }) => {
  const permissions = ["Permissions.Tickets.View", "Permissions.Tickets.Delete"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: string[] = [];
  await page.route("**/api/v1/tickets**", route => { requests.push(route.request().url()); return route.abort(); });
  await page.goto("/tickets/trash");
  await expect(page.getByRole("heading", { name: en.common.forbiddenTitle })).toBeVisible();
  await expect(page.locator('a[href="/tickets/trash"]')).toHaveCount(0);
  expect(requests).toEqual([]);
});
