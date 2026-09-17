import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Impersonation.View";
const support = "Permissions.Users.Impersonate";
const grant = {
  id: "original-grant", jti: "original-jti", actorUserId: TEST_USER.sub, actorTenantId: "root",
  impersonatedUserId: "alice", impersonatedUserName: "alice@acme.example",
  impersonatedTenantId: "acme", reason: "Original reason", startedAtUtc: "2026-09-17T00:00:00Z",
  expiresAtUtc: "2027-01-01T00:00:00Z", status: "Active",
};
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
}

for (const permitted of [false, true]) {
  test(`reopen requires support permission and original actor: ${permitted}`, async ({ page }) => {
    await setup(page, permitted ? [view, support] : [view]);
    let starts = 0;
    page.on("request", request => { if (request.url().endsWith("/impersonation/start")) starts++; });
    await page.route("**/api/v1/identity/impersonation/grants?*", route => route.fulfill({ json: [
      grant, { ...grant, id: "another", actorUserId: "other-operator", impersonatedUserName: "other-target" },
    ] }));
    await page.goto("/impersonation");
    await expect(page.getByText("other-target", { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Re-open", exact: true })).toHaveCount(permitted ? 1 : 0);
    expect(starts).toBe(0);
  });
}

for (const entry of ["/impersonation", "/tenants/acme"]) {
test(`reopen from ${entry} preselects target, retries denied issuance and resets after closing`, async ({ page }) => {
  await setup(page, [view, support, "Permissions.Tenants.View"]);
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01" } }));
  await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ status: 404, json: {} }));
  const forbiddenRequests: string[] = [];
  page.on("request", request => {
    if (request.url().includes("/api/v1/") && /\/users\/search|\/impersonation\/users|\/revoke/.test(request.url())) forbiddenRequests.push(request.url());
  });
  await page.route("**/api/v1/identity/impersonation/grants?*", route => route.fulfill({ json: [grant] }));
  let calls = 0;
  await page.route("**/api/v1/identity/impersonation/start", async route => {
    calls++;
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ targetUserId: "alice", targetTenantId: "acme", reason: "New support reason", durationMinutes: 15 });
    await route.fulfill(calls === 1 ? { status: 403, json: { detail: "Target no longer available" } }
      : { json: { accessToken: "fresh-grant-token", accessTokenExpiresAt: "2027-01-01T00:00:00Z", impersonatedTenantId: "acme" } });
  });
  await page.goto(entry);
  await page.getByRole("button", { name: "Re-open", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("alice@acme.example", { exact: true })).toBeVisible();
  await expect(page.locator("#impersonation-reason")).toHaveValue("");
  await page.locator("#impersonation-reason").fill("New support reason");
  await dialog.getByRole("button", { name: "Start 15-min impersonation", exact: true }).click();
  await expect(page.getByText("Target no longer available", { exact: true })).toBeVisible();
  await expect(page.locator("#impersonation-reason")).toHaveValue("New support reason");
  await dialog.getByRole("button", { name: "Start 15-min impersonation", exact: true }).click();
  const link = dialog.getByRole("link", { name: "Open customer portal", exact: true });
  await expect(link).toBeVisible();
  expect(new URL((await link.getAttribute("href"))!).hash).toContain("token=fresh-grant-token");
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
  await page.getByRole("button", { name: "Re-open", exact: true }).click();
  await expect(page.locator("#impersonation-reason")).toHaveValue("");
  await expect(link).toHaveCount(0);
  expect(calls).toBe(2);
  expect(forbiddenRequests).toEqual([]);
});
}
