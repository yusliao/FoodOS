import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../tests/helpers/auth-seed";
import { installAdminShellMocks, paged } from "../tests/helpers/shell-mocks";

const dashboard = "http://localhost:5195";
const permissions = ["Permissions.Tenants.View", "Permissions.Users.Impersonate"];

for (const endStatus of [200, 500]) {
  test(`admin opens real dashboard and stays isolated when customer session ends ${endStatus}`, async ({ page, context }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.route("**/config.json", route => route.fulfill({ json: { apiBase: "", defaultTenant: "root", dashboardUrl: dashboard } }));
    await page.route("**/api/v1/tenants/acme/status", route => route.fulfill({ json: { id: "acme", name: "Acme", isActive: true, validUpto: "2027-01-01" } }));
    await page.route("**/api/v1/tenants/acme/provisioning", route => route.fulfill({ status: 404, json: {} }));
    await page.route("**/api/v1/identity/impersonation/users?*", route => route.fulfill({ json: paged([{ id: "alice", userName: "alice", email: "alice@acme.example", isActive: true }]) }));

    const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
    const token = `${encode({ alg: "HS256" })}.${encode({ sub: "alice", name: "Alice", tenant: "acme", business_actor: "customer", act_sub: TEST_USER.sub, act_tenant: "root", exp: Math.floor(Date.now() / 1000) + 900 })}.mock-signature`;
    let starts = 0;
    let ends = 0;
    let customerReads = 0;
    await page.route("**/api/v1/identity/impersonation/start", route => {
      starts++;
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().postDataJSON()).toEqual({ targetUserId: "alice", targetTenantId: "acme", reason: "Cross app verification", durationMinutes: 15 });
      return route.fulfill({ json: { accessToken: token, accessTokenExpiresAt: new Date(Date.now() + 900000).toISOString(), impersonatedTenantId: "acme" } });
    });
    await context.route(`${dashboard}/config.json`, route => route.fulfill({ json: { apiBase: "", defaultTenant: "acme" } }));
    await context.route(`${dashboard}/api/v1/**`, route => {
      const path = new URL(route.request().url()).pathname;
      if (path.startsWith("/api/v1/identity/")) {
        expect(route.request().headers().authorization).toBe(`Bearer ${token}`);
        expect(route.request().headers().tenant).toBe("acme");
      }
      if (path.endsWith("/impersonation/end")) {
        ends++;
        return route.fulfill({ status: endStatus, json: {} });
      }
      if (path.endsWith("/permissions")) { customerReads++; return route.fulfill({ json: [] }); }
      if (path.endsWith("/profile")) return route.fulfill({ json: { id: "alice", userName: "alice", firstName: "Alice", email: "alice@acme.example", isActive: true } });
      if (path.endsWith("/me/status")) return route.fulfill({ json: { id: "acme", isActive: true, expiryState: "Active", validUpto: "2027-01-01" } });
      // No permissions to read other business data. Any incidental shell query is denied.
      return route.fulfill({ status: 403, json: { title: "Forbidden", status: 403 } });
    });
    const leakedUrls: string[] = [];
    context.on("request", request => {
      if (request.url().includes(token) || (request.headers().referer ?? "").includes(token)) leakedUrls.push(request.url());
    });
    await page.goto("/tenants/acme");
    const originalAdminToken = await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"));
    await page.getByRole("button", { name: "Impersonate user", exact: true }).click();
    await page.getByRole("button", { name: /alice@acme.example/ }).click();
    await page.locator("#impersonation-reason").fill("Cross app verification");
    await page.getByRole("button", { name: "Start 15-min impersonation", exact: true }).click();
    const opened = context.waitForEvent("page");
    await page.getByRole("link", { name: "Open customer portal", exact: true }).click();
    const portal = await opened;
    await expect(portal.getByRole("status", { name: "Impersonation session", exact: true })).toBeVisible();
    expect(new URL(portal.url()).origin).toBe(dashboard);
    expect(new URL(portal.url()).hash).toBe("");
    expect(await portal.evaluate(() => window.opener === null)).toBe(true);
    expect(await portal.evaluate(() => localStorage.getItem("fsh.dashboard.accessToken"))).toBe(token);
    expect(await portal.evaluate(() => localStorage.getItem("fsh.dashboard.refreshToken"))).toBeNull();
    expect(await portal.evaluate(() => localStorage.getItem("fsh.admin.accessToken"))).toBeNull();
    await expect.poll(() => customerReads).toBeGreaterThan(0);
    await portal.getByRole("button", { name: "End impersonation", exact: true }).click();
    await expect(portal).toHaveURL(`${dashboard}/login`);
    await expect.poll(() => ends).toBe(1);
    expect(await portal.evaluate(() => localStorage.getItem("fsh.dashboard.accessToken"))).toBeNull();
    expect(await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"))).toBe(originalAdminToken);
    expect(await page.evaluate(() => localStorage.getItem("fsh.dashboard.accessToken"))).toBeNull();
    expect(starts).toBe(1);
    expect(leakedUrls).toEqual([]);
  });
}
