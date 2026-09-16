import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, ADMIN_PERMS } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

function token(tenant = "root", expired = false) {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return [encode({ alg: "HS256" }), encode({ sub: TEST_USER.sub, tenant,
    business_actor: tenant === "root" ? "operator" : "customer", name: "Warehouse Employee",
    exp: Math.floor(Date.now() / 1000) + (expired ? -3600 : 3600) }), "sig"].join(".");
}

test("ordinary root employee can sign in without an Admin role", async ({ page }) => {
  await installAdminShellMocks(page, []);
  await mockJsonResponse(page, "**/api/v1/identity/token/issue", { accessToken: token(), refreshToken: "refresh" });
  await page.goto("/login");
  await page.getByLabel("Email").fill("warehouse@root.test");
  await page.getByLabel("Password", { exact: true }).fill("TestPassword!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("main").getByRole("heading", { name: "Operator workbench" })).toBeVisible();
  await expect(page.getByText("Warehouse Employee · Operator employee · root")).toBeVisible();
});

test("customer token returned from login is never admitted", async ({ page }) => {
  await mockJsonResponse(page, "**/api/v1/identity/token/issue", { accessToken: token("acme"), refreshToken: "refresh" });
  await page.goto("/login");
  await page.getByLabel("Email").fill("customer@acme.test");
  await page.getByLabel("Password", { exact: true }).fill("TestPassword!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#login-error")).toContainText("operator employees only");
  await expect(page).toHaveURL(/\/login$/);
  expect(await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"))).toBeNull();
});

test("stored restaurant session cannot be restored into admin", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, tenant: "acme", permissions: [...ADMIN_PERMS] });
  const requests: string[] = [];
  page.on("request", request => { if (request.url().includes("/api/v1/")) requests.push(request.url()); });
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  expect(requests).toEqual([]);
});

test("refresh returning a customer identity clears the session", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await page.addInitScript(access => localStorage.setItem("fsh.admin.accessToken", access), token("root", true));
  await mockJsonResponse(page, "**/api/v1/identity/token/refresh", { token: token("acme"), refreshToken: "rotated" });
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  expect(await page.evaluate(() => localStorage.getItem("fsh.admin.accessToken"))).toBeNull();
});

test("cross-tab customer token replaces the operator session with login, not customer chrome", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installAdminShellMocks(page, []);
  await page.goto("/");
  await expect(page.getByRole("main")).toBeVisible();
  await page.evaluate(access => {
    localStorage.setItem("fsh.admin.accessToken", access);
    window.dispatchEvent(new StorageEvent("storage", { key: "fsh.admin.accessToken", newValue: access }));
  }, token("acme"));
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("main")).toHaveCount(0);
});

test("permission failure does not fall back to cached administrator permissions", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [...ADMIN_PERMS] });
  await installAdminShellMocks(page);
  await page.route("**/api/v1/identity/permissions", route => route.fulfill({ status: 403, contentType: "application/json", body: "{}" }));
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText("Your role permissions could not be verified");
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem("fsh.admin.permissions") ?? "null"))).toEqual([]);
  await mockJsonResponse(page, "**/api/v1/identity/permissions", ["Permissions.Users.View"]);
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("main").getByRole("link", { name: "Employees", exact: true })).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
});

test("cached roles cannot render a protected page while server permissions are pending", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [...ADMIN_PERMS] });
  await installAdminShellMocks(page);
  let release: (() => void) | undefined;
  await page.route("**/api/v1/identity/permissions", async route => {
    await new Promise<void>(resolve => { release = resolve; });
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });
  await page.goto("/users");
  await expect.poll(() => Boolean(release)).toBe(true);
  await expect(page.getByRole("main")).toHaveCount(0);
  release!();
  await expect(page.getByRole("main")).toContainText(/Forbidden|Access denied/i);
});
