import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Users.View";
const account = { id: "employee", userName: "employee", firstName: "Test", lastName: "Employee", email: "employee@root.test", isActive: true, emailConfirmed: true };
const roles = [{ roleId: "basic", roleName: "Basic", enabled: true }, { roleId: "manager", roleName: "Manager", enabled: false }];

test("employee directory query failure offers explicit retry", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [view] });
  await installAdminShellMocks(page, [view]);
  let fail = true;
  await page.route("**/api/v1/identity/users/search?*", route => route.fulfill(fail
    ? { status: 403, json: { detail: "Directory denied" } }
    : { json: paged([account]) }));
  await page.goto("/users");
  await expect(page.getByText("Directory denied")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Test Employee/ })).toBeVisible();
});
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  let active = true;
  let assigned = roles.map(role => ({ ...role }));
  const requests: { path: string; method: string; body: unknown }[] = [];
  await page.route("**/identity/users/employee**", async route => {
    const req = route.request();
    expect(req.headers().tenant).toBe("root");
    const path = new URL(req.url()).pathname;
    requests.push({ path, method: req.method(), body: req.postData() ? req.postDataJSON() : null });
    if (path.endsWith("/sessions")) return route.fulfill({ json: [] });
    if (path.endsWith("/roles")) {
      if (req.method() === "POST") assigned = req.postDataJSON().userRoles;
      return route.fulfill({ json: req.method() === "GET" ? assigned : "Updated" });
    }
    if (req.method() === "PATCH") active = req.postDataJSON().activateUser;
    return route.fulfill({ json: { ...account, isActive: active } });
  });
  return requests;
}

test("viewer cannot mutate accounts or roles and does not query sessions", async ({ page }) => {
  const requests = await setup(page, [view]);
  await page.goto("/users/employee");
  await expect(page.getByRole("button", { name: "Toggle Basic", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: /Deactivate account|Save changes/ })).toHaveCount(0);
  expect(requests.every(req => req.method === "GET" && !req.path.endsWith("/sessions"))).toBe(true);
});

test("write permissions without View cannot bypass direct URL", async ({ page }) => {
  const requests = await setup(page, ["Permissions.Users.Update", "Permissions.Users.ManageRoles"]);
  await page.goto("/users/employee");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toEqual([]);
});

test("Update can deactivate and reactivate but cannot assign roles", async ({ page }) => {
  const requests = await setup(page, [view, "Permissions.Users.Update"]);
  await page.goto("/users/employee");
  await page.getByRole("button", { name: "Deactivate account", exact: true }).click();
  await page.getByRole("button", { name: "Activate account", exact: true }).click();
  await expect(page.getByRole("button", { name: "Deactivate account", exact: true })).toBeEnabled();
  expect(requests.filter(req => req.method === "PATCH").map(req => req.body)).toEqual([
    { userId: "employee", activateUser: false }, { userId: "employee", activateUser: true },
  ]);
  await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toBeDisabled();
});

test("ManageRoles saves the full set and preserves draft on rejected write", async ({ page }) => {
  const requests = await setup(page, [view, "Permissions.Users.ManageRoles"]);
  let fail = true;
  await page.route("**/api/v1/identity/users/employee/roles", async route => {
    if (route.request().method() !== "POST" || !fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Role assignment rejected" } });
  });
  await page.goto("/users/employee");
  await expect(page.getByRole("button", { name: "Deactivate account", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Toggle Manager", exact: true }).click();
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByText("Role assignment rejected")).toBeVisible();
  await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toHaveAttribute("aria-pressed", "true");
  fail = false;
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toBeDisabled();
  expect(requests.find(req => req.method === "POST")?.body).toEqual({ userId: "employee", userRoles: roles.map(role => ({ ...role, enabled: true })) });
});

for (const endpoint of ["employee", "employee/roles"]) test(`${endpoint} query failure retries without exposing save actions`, async ({ page }) => {
  await setup(page, [view, "Permissions.Users.ManageRoles"]);
  let fail = true;
  await page.route(`**/api/v1/identity/users/${endpoint}`, async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Employee query rejected" } });
  });
  await page.goto("/users/employee");
  await expect(page.getByText("Employee query rejected")).toBeVisible();
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toBeEnabled();
});

test("status failure preserves current state for explicit retry", async ({ page }) => {
  await setup(page, [view, "Permissions.Users.Update"]);
  let fail = true;
  await page.route("**/api/v1/identity/users/employee", async route => {
    if (route.request().method() !== "PATCH" || !fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Status update rejected" } });
  });
  await page.goto("/users/employee");
  await page.getByRole("button", { name: "Deactivate account", exact: true }).click();
  await expect(page.getByText("Status update rejected")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Deactivate account", exact: true }).click();
  await expect(page.getByRole("button", { name: "Activate account", exact: true })).toBeEnabled();
});

test("session view is independent and Chinese mobile read-only layout fits", async ({ page }) => {
  const requests = await setup(page, [view, "Permissions.Sessions.ViewAll"]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/users/employee");
  await expect.poll(() => requests.some(req => req.path.endsWith("/sessions"))).toBe(true);
  await expect(page.getByRole("button", { name: /Manager/ })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  expect(requests.every(req => req.method === "GET")).toBe(true);
});

test("role loading and empty result do not expose a save action", async ({ page }) => {
  await setup(page, [view, "Permissions.Users.ManageRoles"]);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/identity/users/employee/roles", async route => { await gate; await route.fulfill({ json: [] }); });
  await page.goto("/users/employee");
  try {
    await expect(page.getByRole("heading", { name: "Role assignment", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Save changes", exact: true })).toHaveCount(0);
  } finally { release(); }
  await expect(page.getByText("No roles defined for this tenant.", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toHaveCount(0);
});

test("pending role write locks toggles and save until refresh completes", async ({ page }) => {
  await setup(page, [view, "Permissions.Users.ManageRoles"]);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/identity/users/employee/roles", async route => {
    if (route.request().method() === "POST") await gate;
    await route.fallback();
  });
  await page.goto("/users/employee");
  await page.getByRole("button", { name: "Toggle Manager", exact: true }).click();
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  try {
    await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toBeDisabled();
    await expect(page.getByRole("button", { name: /^Saving/ })).toBeDisabled();
  } finally { release(); }
  await expect(page.getByRole("button", { name: "Toggle Manager", exact: true })).toBeEnabled();
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toBeDisabled();
});
