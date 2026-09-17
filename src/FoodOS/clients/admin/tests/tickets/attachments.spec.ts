import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };
const id = "33333333-3333-3333-3333-333333333333";
const own = { id: "44444444-4444-4444-4444-444444444444", ownerType: "Ticket", ownerId: id, originalFileName: "own.pdf", contentType: "application/pdf", sizeBytes: 123, visibility: "Private", status: "Available", createdByUserId: TEST_USER.sub };
const foreign = { ...own, id: "55555555-5555-5555-5555-555555555555", originalFileName: "customer.pdf", createdByUserId: "customer-user" };
const ticket = { id, number: "TK-1", title: "Evidence ticket", status: "Open", priority: "Low", customerTenantId: "acme", reporterUserId: "reporter", createdAtUtc: "2026-09-17T00:00:00Z", commentCount: 0 };
for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  test(`${culture} private attachment read retries, paginates and signs only on demand`, async ({ page }) => {
    const permissions = ["Permissions.Tickets.View"];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
    await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
    let failed = true;
    await page.route(`**/api/v1/files/owners/Ticket/${id}?**`, route => {
      expect(route.request().headers().tenant).toBe("root");
      if (failed) return route.fulfill({ status: 403, json: { detail: "Attachment list denied" } });
      const second = new URL(route.request().url()).searchParams.get("pageNumber") === "2";
      return route.fulfill({ json: paged([second ? foreign : own], { pageNumber: second ? 2 : 1, totalPages: 2, totalCount: 2 }) });
    });
    let links = 0;
    await page.route(`**/api/v1/files/${foreign.id}/url`, route => {
      expect(route.request().headers().tenant).toBe("root");
      links++;
      return route.fulfill(links === 1 ? { status: 404, json: { detail: "File unavailable" } } : { json: { url: "https://storage.example/evidence?signature=private", expiresAt: new Date(Date.now() + 60000).toISOString() } });
    });
    await page.goto(`/tickets/${id}`);
    const section = page.getByRole("region", { name: m.tickets.attachments });
    await expect(section.getByRole("alert")).toContainText("Attachment list denied");
    failed = false;
    await section.getByRole("button", { name: m.workbench.retry }).click();
    await expect(section.getByText("own.pdf")).toBeVisible();
    expect(links).toBe(0);
    await expect(section.getByRole("button", { name: m.tickets.deleteAttachment })).toHaveCount(0);
    await section.getByRole("button", { name: m.common.next }).click();
    await expect(section.getByText("customer.pdf")).toBeVisible();
    await section.getByRole("button", { name: m.tickets.prepareDownload }).click();
    await expect(section.getByRole("alert")).toContainText("File unavailable");
    await section.getByRole("button", { name: m.tickets.prepareDownload }).click();
    const link = section.getByRole("link", { name: m.tickets.downloadAttachment });
    await expect(link).toHaveAttribute("href", "https://storage.example/evidence?signature=private");
    await expect(link).toHaveAttribute("rel", "noopener noreferrer");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });

  test(`${culture} only own attachment deletion is offered and failure can cancel and retry`, async ({ page }) => {
    const permissions = ["Permissions.Tickets.View", "Permissions.Files.DeleteOwn"];
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
    await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
    let writes = 0;
    await page.route(`**/api/v1/files/owners/Ticket/${id}?**`, route => route.fulfill({ json: paged(writes > 1 ? [foreign] : [own, foreign]) }));
    let release!: () => void;
    const held = new Promise<void>(resolve => { release = resolve; });
    await page.route(`**/api/v1/files/${own.id}`, async route => {
      expect(route.request().method()).toBe("DELETE");
      expect(route.request().headers().tenant).toBe("root");
      writes++;
      if (writes === 1) return route.fulfill({ status: 403, json: { detail: "Deletion denied" } });
      await held;
      return route.fulfill({ status: 204 });
    });
    await page.goto(`/tickets/${id}`);
    const section = page.getByRole("region", { name: m.tickets.attachments });
    const remove = section.getByRole("button", { name: m.tickets.deleteAttachment });
    await expect(remove).toHaveCount(1);
    await remove.click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: m.tickets.deleteAttachment }).click();
    await expect(dialog.getByRole("alert")).toContainText("Deletion denied");
    await dialog.getByRole("button", { name: m.chrome.cancel, exact: true }).click();
    await remove.click();
    await expect(dialog.getByRole("alert")).toHaveCount(0);
    await dialog.getByRole("button", { name: m.tickets.deleteAttachment }).click();
    await expect(dialog.getByRole("button", { name: m.chrome.cancel, exact: true })).toBeDisabled();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    release();
    await expect(dialog).toHaveCount(0);
    await expect(section.getByText("own.pdf")).toHaveCount(0);
    await expect(section.getByText("customer.pdf")).toBeVisible();
  });
}

test("file permissions without ticket View never request attachments", async ({ page }) => {
  const permissions = ["Permissions.Files.Upload", "Permissions.Files.DeleteOwn"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: string[] = [];
  await page.route("**/api/v1/files/**", route => { requests.push(route.request().url()); return route.fulfill({ status: 500 }); });
  await page.goto(`/tickets/${id}`);
  await expect(page.getByRole("heading", { name: en.common.forbiddenTitle })).toBeVisible();
  expect(requests).toEqual([]);
});
