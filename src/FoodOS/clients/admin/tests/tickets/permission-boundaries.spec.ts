import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
const id = "33333333-3333-3333-3333-333333333333";
const ticket = { id, number: "TK-1", title: "Boundary ticket", status: "Open", priority: "Low", customerTenantId: "acme", reporterUserId: "reporter", createdAtUtc: "2026-09-17T00:00:00Z", commentCount: 0 };

for (const action of ["delete", "restore"] as const) test(`ticket ${action} cancellation clears failed confirmation without a second write`, async ({ page }) => {
  const permissions = action === "delete" ? ["Permissions.Tickets.View", "Permissions.Tickets.Delete"] : ["Permissions.Tickets.Restore"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
  await page.route("**/api/v1/tickets/trash?**", route => route.fulfill({ json: paged([ticket]) }));
  const keys: string[] = [];
  await page.route(action === "delete" ? `**/api/v1/tickets/${id}` : `**/api/v1/tickets/${id}/restore`, route => {
    if (route.request().method() === "GET") return route.fallback();
    keys.push(route.request().headers()["idempotency-key"] ?? "");
    return route.fulfill({ status: 403, json: { detail: "Confirmation rejected" } });
  });
  await page.goto(action === "delete" ? `/tickets/${id}` : "/tickets/trash");
  await page.getByRole("button", { name: en.tickets[action], exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: en.tickets[action], exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Confirmation rejected");
  await dialog.getByRole("button", { name: en.chrome.cancel, exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await page.getByRole("button", { name: en.tickets[action], exact: true }).click();
  await expect(dialog.getByRole("alert")).toHaveCount(0);
  expect(keys).toHaveLength(1);
  await dialog.getByRole("button", { name: en.tickets[action], exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Confirmation rejected");
  expect(keys).toHaveLength(2);
  if (action === "restore") { expect(keys[0]).toBeTruthy(); expect(keys[1]).not.toBe(keys[0]); }
});

test("upload-url rejection does not upload bytes or finalize and preserves selected file", async ({ page }) => {
  const permissions = ["Permissions.Tickets.View", "Permissions.Files.Upload"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
  const writes: string[] = [];
  page.on("request", request => { if (request.method() !== "GET" && request.url().includes("/api/v1/files")) writes.push(request.url()); });
  await page.route("**/api/v1/files/upload-url", route => route.fulfill({ status: 403, json: { detail: "Upload permission revoked" } }));
  await page.goto(`/tickets/${id}`);
  const input = page.getByLabel(en.tickets.chooseAttachment);
  await input.setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
  await page.getByRole("button", { name: en.tickets.uploadAttachment, exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(en.tickets.uploadFailed);
  await expect(input).toBeEnabled();
  expect(await input.evaluate((element: HTMLInputElement) => element.files?.[0]?.name)).toBe("evidence.pdf");
  expect(writes).toHaveLength(1);
  expect(writes[0]).toContain("/upload-url");
});

test("permission rehydration removes ticket access and cancels a prepared upload's next stages", async ({ page }) => {
  let permissions = ["Permissions.Tickets.View", "Permissions.Files.Upload"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/identity/permissions", route => route.fulfill({ json: permissions }));
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/files/upload-url", async route => {
    await held;
    return route.fulfill({ json: { fileAssetId: id, uploadUrl: "https://storage.example/held", requiredHeaders: {}, expiresAt: new Date(Date.now() + 60000).toISOString() } });
  });
  const unexpected: string[] = [];
  await page.route("https://storage.example/**", route => { unexpected.push("put"); return route.fulfill({ status: 200 }); });
  await page.route(`**/api/v1/files/${id}/finalize`, route => { unexpected.push("finalize"); return route.fulfill({ status: 403 }); });
  await page.goto(`/tickets/${id}`);
  await page.getByLabel(en.tickets.chooseAttachment).setInputFiles({ name: "evidence.pdf", mimeType: "application/pdf", buffer: Buffer.from("test") });
  await page.getByRole("button", { name: en.tickets.uploadAttachment, exact: true }).click();
  await expect(page.getByLabel(en.tickets.chooseAttachment)).toBeDisabled();
  permissions = [];
  await page.evaluate(() => window.dispatchEvent(new StorageEvent("storage", { key: "fsh.admin.accessToken" })));
  await expect(page.getByRole("heading", { name: en.common.forbiddenTitle })).toBeVisible();
  const finished = page.waitForResponse("**/api/v1/files/upload-url");
  release();
  await finished;
  await expect(page.getByLabel(en.tickets.chooseAttachment)).toHaveCount(0);
  expect(unexpected).toEqual([]);
});
