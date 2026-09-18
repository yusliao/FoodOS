import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };
const id = "33333333-3333-3333-3333-333333333333";
const fileId = "44444444-4444-4444-4444-444444444444";
const dto = { id: fileId, ownerType: "Ticket", ownerId: id, originalFileName: "evidence.pdf", contentType: "application/pdf", sizeBytes: 4, visibility: "Private", status: "Available", createdByUserId: TEST_USER.sub };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: { id, number: "TK-1", title: "Evidence", status: "Open", priority: "Low", customerTenantId: "acme", reporterUserId: "reporter", createdAtUtc: "2026-09-17T00:00:00Z", commentCount: 0 } }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
}
for (const [culture, m] of [["en-US", en], ["zh-CN", zh]] as const) {
  for (const mode of ["put", "finalize", "lost-response"] as const) test(`${culture} upload recovers ${mode} without allocating another file`, async ({ page }) => {
    await setup(page, ["Permissions.Tickets.View", "Permissions.Files.Upload"]);
    await page.addInitScript(value => localStorage.setItem("foodos.culture", value), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let creates = 0, puts = 0, finalizes = 0, checks = 0;
    let available = false;
    await page.route(`**/api/v1/files/owners/Ticket/${id}?**`, route => route.fulfill({ json: paged(available ? [dto] : []) }));
    await page.route("**/api/v1/files/upload-url", route => {
      creates++;
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().postDataJSON()).toEqual({ ownerType: "Ticket", ownerId: id, fileName: "evidence.pdf", contentType: "application/pdf", sizeBytes: 4, visibility: "Private", category: "Document" });
      return route.fulfill({ json: { fileAssetId: fileId, uploadUrl: new URL("/storage-put", page.url()).href, requiredHeaders: { "Content-Type": "application/pdf" }, expiresAt: new Date(Date.now() + 60000).toISOString() } });
    });
    await page.route("**/storage-put", route => {
      puts++;
      expect(route.request().method()).toBe("PUT");
      expect(route.request().headers().authorization).toBeUndefined();
      expect(route.request().headers().tenant).toBeUndefined();
      return route.fulfill({ status: mode === "put" && puts === 1 ? 500 : 200 });
    });
    await page.route(`**/api/v1/files/${fileId}`, route => {
      checks++;
      expect(route.request().headers().tenant).toBe("root");
      return route.fulfill({ json: { ...dto, status: available ? "Available" : "PendingUpload" } });
    });
    await page.route(`**/api/v1/files/${fileId}/finalize`, route => {
      finalizes++;
      expect(route.request().headers().tenant).toBe("root");
      if (finalizes === 1 && mode !== "put") {
        available = mode === "lost-response";
        return route.fulfill({ status: 500, json: { detail: "Uncertain finalize" } });
      }
      available = true;
      return route.fulfill({ json: dto });
    });
    await page.goto(`/tickets/${id}`);
    const section = page.getByRole("region", { name: m.tickets.attachments });
    await section.getByLabel(m.tickets.chooseAttachment).setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
    await section.getByRole("button", { name: m.tickets.uploadAttachment, exact: true }).click();
    await expect(section.getByRole("alert")).toContainText(m.tickets.uploadFailed);
    await section.getByRole("button", { name: m.tickets.retryUpload }).click();
    await expect(section.getByText(m.tickets.uploadDone, { exact: true })).toBeVisible();
    await expect(section.getByRole("heading", { name: "evidence.pdf" })).toBeVisible();
    expect(creates).toBe(1);
    expect(checks).toBe(1);
    expect(puts).toBe(mode === "put" ? 2 : 1);
    expect(finalizes).toBe(mode === "finalize" ? 2 : 1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
for (const mode of ["expired", "quarantined"] as const) test(`upload ${mode} recovery never creates or finalizes another asset`, async ({ page }) => {
  await setup(page, ["Permissions.Tickets.View", "Permissions.Files.Upload"]);
  let creates = 0, puts = 0, finalizes = 0;
  await page.route("**/api/v1/files/upload-url", route => { creates++; return route.fulfill({ json: { fileAssetId: fileId, uploadUrl: new URL("/storage-put", page.url()).href, requiredHeaders: {}, expiresAt: "2000-01-01T00:00:00Z" } }); });
  await page.route("**/storage-put", route => { puts++; return route.fulfill({ status: 403 }); });
  await page.route(`**/api/v1/files/${fileId}`, route => route.fulfill({ json: { ...dto, status: mode === "expired" ? "PendingUpload" : "Quarantined" } }));
  await page.route(`**/api/v1/files/${fileId}/finalize`, route => { finalizes++; return route.fulfill({ status: 500 }); });
  await page.goto(`/tickets/${id}`);
  await page.getByLabel(en.tickets.chooseAttachment).setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
  await page.getByRole("button", { name: en.tickets.uploadAttachment, exact: true }).click();
  await expect(page.getByRole("button", { name: en.tickets.retryUpload })).toBeEnabled();
  await page.getByRole("button", { name: en.tickets.retryUpload }).click();
  await expect(page.getByRole("button", { name: en.tickets.retryUpload })).toBeEnabled();
  expect(creates).toBe(1); expect(puts).toBe(1); expect(finalizes).toBe(0);
});

test("read-only ticket viewer never receives an upload control", async ({ page }) => {
  await setup(page, ["Permissions.Tickets.View"]);
  await page.goto(`/tickets/${id}`);
  await expect(page.getByRole("heading", { name: en.tickets.attachments })).toBeVisible();
  await expect(page.getByLabel(en.tickets.chooseAttachment)).toHaveCount(0);
});

test("upload preparation freezes controls and leaving the ticket stops the next stage", async ({ page }) => {
  await setup(page, ["Permissions.Tickets.View", "Permissions.Files.Upload"]);
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  const nextStages: string[] = [];
  await page.route("**/api/v1/files/upload-url", async route => {
    await held;
    return route.fulfill({ json: { fileAssetId: fileId, uploadUrl: "https://storage.example/held", requiredHeaders: {}, expiresAt: new Date(Date.now() + 60000).toISOString() } });
  });
  await page.route("https://storage.example/**", route => { nextStages.push("put"); return route.fulfill({ status: 200 }); });
  await page.route(`**/api/v1/files/${fileId}/finalize`, route => { nextStages.push("finalize"); return route.fulfill({ json: dto }); });
  await page.route("**/api/v1/tickets?**", route => route.fulfill({ json: paged([]) }));
  await page.goto(`/tickets/${id}`);
  const input = page.getByLabel(en.tickets.chooseAttachment);
  await input.setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
  await page.getByRole("button", { name: en.tickets.uploadAttachment, exact: true }).click();
  await expect(input).toBeDisabled();
  await expect(page.getByRole("button", { name: en.tickets.uploadAttachment, exact: true })).toBeDisabled();
  await page.getByRole("link", { name: en.tickets.back }).click();
  await expect(page.getByText(en.tickets.empty, { exact: true })).toBeVisible();
  const finished = page.waitForResponse("**/api/v1/files/upload-url");
  release();
  await finished;
  await expect(page.getByRole("heading", { name: en.tickets.title, exact: true })).toBeVisible();
  expect(nextStages).toEqual([]);
});

for (const url of ["javascript:alert(1)", "https://user:password@storage.example/file", "https://storage.example/expired"]) test(`unsafe or expired download is not exposed: ${url}`, async ({ page }) => {
  await setup(page, ["Permissions.Tickets.View"]);
  await page.route(`**/api/v1/files/owners/Ticket/${id}?**`, route => route.fulfill({ json: paged([dto]) }));
  await page.route(`**/api/v1/files/${fileId}/url`, route => route.fulfill({ json: { url, expiresAt: url.endsWith("expired") ? "2000-01-01T00:00:00Z" : new Date(Date.now() + 60000).toISOString() } }));
  await page.goto(`/tickets/${id}`);
  await page.getByRole("button", { name: en.tickets.prepareDownload }).click();
  await expect(page.getByRole("alert")).toContainText(en.tickets.downloadInvalid);
  await expect(page.getByRole("link", { name: en.tickets.downloadAttachment })).toHaveCount(0);
});
