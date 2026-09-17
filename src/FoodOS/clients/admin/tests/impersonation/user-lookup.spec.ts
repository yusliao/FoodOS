import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const support = "Permissions.Users.Impersonate";
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01" } }));
  await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ status: 404, json: {} }));
}

test("support-only operator uses explicit target and root identity without ordinary user search", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", support]);
  const ordinary: string[] = [];
  page.on("request", r => { if (r.url().includes("/users/search")) ordinary.push(r.url()); });
  await page.route("**/api/v1/identity/impersonation/users?*", route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(new URL(route.request().url()).searchParams.get("targetTenantId")).toBe("acme");
    return route.fulfill({ json: paged([{ id: "alice", userName: "alice", email: "alice@acme.example", isActive: true }]) });
  });
  await page.goto("/tenants/acme");
  await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
  await expect(page.getByText("alice@acme.example", { exact: true })).toBeVisible();
  expect(ordinary).toEqual([]);
});

test("lookup failure retries, search empty is not a failure", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", support]);
  let failed = true;
  await page.route("**/api/v1/identity/impersonation/users?*", route => route.fulfill(failed
    ? { status: 403, json: { detail: "Target unavailable" } } : { json: paged([]) }));
  await page.goto("/tenants/acme");
  await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
  await expect(page.getByText("Target unavailable", { exact: true })).toBeVisible();
  failed = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("Target unavailable", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("dialog").getByRole("button", { name: "Cancel", exact: true })).toBeVisible();
});

test("ordinary user-search permission does not permit support lookup", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", "Permissions.Users.Search"]);
  let calls = 0;
  page.on("request", r => { if (r.url().includes("/impersonation/users")) calls++; });
  await page.goto("/tenants/acme");
  await expect(page.getByRole("heading", { name: "Acme", level: 1 })).toBeVisible();
  await expect(page.getByRole("button", { name: "Impersonate user", exact: true })).toHaveCount(0);
  expect(calls).toBe(0);
});

test("issuance freezes inputs, preserves a failed reason and exposes one reusable fragment link", async ({ page }) => {
  await setup(page, ["Permissions.Tenants.View", support]);
  await page.route("**/api/v1/identity/impersonation/users?*", route => route.fulfill({
    json: paged([{ id: "alice", userName: "alice", email: "alice@acme.example", isActive: true }]),
  }));
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let calls = 0;
  await page.route("**/api/v1/identity/impersonation/start", async route => {
    calls++;
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().postDataJSON()).toEqual({ targetUserId: "alice", targetTenantId: "acme", reason: "Support ticket", durationMinutes: 15 });
    if (calls === 1) {
      await gate;
      await route.fulfill({ status: 403, json: { detail: "Support denied" } });
    } else await route.fulfill({ json: { accessToken: "test-support-token", accessTokenExpiresAt: "2027-01-01T00:00:00Z", impersonatedTenantId: "acme" } });
  });
  await page.goto("/tenants/acme");
  const tokenBefore = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"));
  await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
  await page.getByRole("button", { name: /alice@acme.example/ }).click();
  const start = page.getByRole("button", { name: "Start 15-min impersonation", exact: true });
  await expect(start).toBeDisabled();
  await page.locator("#impersonation-reason").fill("Support ticket");
  await start.click();
  await expect(page.locator("#impersonation-reason")).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toBeVisible();
  release();
  await expect(page.getByText("Support denied", { exact: true })).toBeVisible();
  await expect(page.locator("#impersonation-reason")).toHaveValue("Support ticket");
  await start.click();
  const link = page.getByRole("link", { name: "Open customer portal", exact: true });
  await expect(link).toBeVisible();
  const url = new URL((await link.getAttribute("href"))!);
  expect(url.search).toBe("");
  expect(url.hash).toContain("token=test-support-token");
  expect(url.hash).toContain("tenant=acme");
  await expect(link).toHaveAttribute("rel", "noopener noreferrer");
  await page.context().route(`${url.origin}/**`, route => {
    expect(route.request().url()).not.toContain("test-support-token");
    return route.fulfill({ contentType: "text/html", body: "<title>Customer portal stub</title>" });
  });
  const opened = page.context().waitForEvent("page");
  await link.click();
  const portal = await opened;
  await portal.waitForLoadState();
  expect(portal.url()).toContain("#impersonate?");
  expect(await portal.evaluate(() => window.opener === null)).toBe(true);
  await portal.close();
  expect(calls).toBe(2);
  expect(await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"))).toBe(tokenBefore);
  await expect(start).toHaveCount(0);
});
