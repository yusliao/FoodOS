import { expect, test, type Page } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";

const accessKey = "fsh.dashboard.accessToken";
const refreshKey = "fsh.dashboard.refreshToken";
const stashKey = "fsh.dashboard.impersonation.actorAccessToken";
function jwt(overrides: Record<string, unknown> = {}) {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${encode({ alg: "HS256", typ: "JWT" })}.${encode({
    sub: "customer-user", tenant: "acme", name: "Support target",
    business_actor: "customer", act_sub: "operator-user", act_tenant: "root",
    exp: Math.floor(Date.now() / 1000) + 3600, ...overrides,
  })}.test-signature`;
}

async function setup(page: Page) {
  await installShellMocks(page);
  const oldToken = jwt({ act_sub: undefined, act_tenant: undefined, sub: "prior-customer" });
  await page.addInitScript(({ oldToken, accessKey, refreshKey, stashKey }) => {
    localStorage.setItem(accessKey, oldToken);
    localStorage.setItem(refreshKey, "prior-refresh");
    localStorage.setItem("fsh.dashboard.tenant", "acme");
    localStorage.setItem(stashKey, "stale-actor-token");
  }, { oldToken, accessKey, refreshKey, stashKey });
  return oldToken;
}

for (const invalid of ["malformed", "expired", "missing-expiry", "tenant-mismatch", "ordinary-token", "expired-link"]) {
  test(`rejects ${invalid} handoff without replacing existing session`, async ({ page }) => {
    const oldToken = await setup(page);
    const token = invalid === "malformed" ? "not-a-jwt" : jwt({
      ...(invalid === "expired" ? { exp: 1 } : {}),
      ...(invalid === "missing-expiry" ? { exp: undefined } : {}),
      ...(invalid === "ordinary-token" ? { act_sub: undefined } : {}),
    });
    const params = new URLSearchParams({ token, tenant: invalid === "tenant-mismatch" ? "other" : "acme" });
    if (invalid === "expired-link") params.set("expiresAt", "2000-01-01T00:00:00Z");
    await page.goto(`/settings/profile#impersonate?${params}`);
    await expect(page).toHaveURL(/\/settings\/profile$/);
    expect(await page.evaluate(key => localStorage.getItem(key), accessKey)).toBe(oldToken);
    expect(await page.evaluate(key => localStorage.getItem(key), refreshKey)).toBe("prior-refresh");
    await expect(page.getByText("Cross-tenant impersonation", { exact: true })).toHaveCount(0);
  });
}

for (const endStatus of [200, 500]) {
  test(`legacy root handoff with an actor stash still signs out on end ${endStatus}`, async ({ page }) => {
    await setup(page);
    const token = jwt();
    await page.addInitScript(({ token, accessKey, refreshKey }) => {
      localStorage.setItem(accessKey, token);
      localStorage.removeItem(refreshKey);
    }, { token, accessKey, refreshKey });
    let endCalls = 0;
    await page.route("**/api/v1/identity/impersonation/end", async route => {
      endCalls++;
      await route.fulfill({ status: endStatus, json: endStatus === 200
        ? { accessToken: jwt({ tenant: "root", business_actor: "operator" }), refreshToken: "root-refresh" }
        : { title: "Unavailable", status: 500 } });
    });
    await page.goto("/settings/profile");
    await expect(page.getByText("Cross-tenant impersonation", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: /end impersonation/i }).click();
    await expect(page).toHaveURL(/\/login$/);
    await expect.poll(() => endCalls).toBe(1);
    expect(await page.evaluate(key => localStorage.getItem(key), accessKey)).toBeNull();
    expect(await page.evaluate(key => localStorage.getItem(key), stashKey)).toBeNull();
  });

  test(`consumes real bootstrap handoff and signs out on end ${endStatus} without restoring an actor`, async ({ page }) => {
    await setup(page);
    const token = jwt();
    let endCalls = 0;
    await page.route("**/api/v1/identity/impersonation/end", async route => {
      endCalls++;
      expect(route.request().headers().authorization).toBe(`Bearer ${token}`);
      expect(route.request().headers().tenant).toBe("acme");
      await route.fulfill({ status: endStatus, json: endStatus === 200
        ? { accessToken: jwt({ tenant: "root", business_actor: "operator" }), refreshToken: "root-refresh" }
        : { title: "Unavailable", status: 500 } });
    });
    const params = new URLSearchParams({ token, tenant: "acme", expiresAt: new Date(Date.now() + 3600000).toISOString() });
    await page.goto(`/settings/profile#impersonate?${params}`);
    await expect(page).toHaveURL(/\/settings\/profile$/);
    await expect(page.getByText("Cross-tenant impersonation", { exact: true })).toBeVisible();
    expect(await page.evaluate(key => localStorage.getItem(key), accessKey)).toBe(token);
    expect(await page.evaluate(key => localStorage.getItem(key), refreshKey)).toBeNull();
    expect(await page.evaluate(key => localStorage.getItem(key), stashKey)).toBeNull();
    await page.getByRole("button", { name: /end impersonation/i }).click();
    await expect(page).toHaveURL(/\/login$/);
    await expect.poll(() => endCalls).toBe(1);
    expect(await page.evaluate(key => localStorage.getItem(key), accessKey)).toBeNull();
    expect(await page.evaluate(key => localStorage.getItem(key), refreshKey)).toBeNull();
  });
}
