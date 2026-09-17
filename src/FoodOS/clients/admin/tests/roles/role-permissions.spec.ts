import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const role = { id: "role-manager", name: "Manager", description: "Operations", permissions: ["Permissions.Users.View", "Permissions.Future.Existing"] };
const view = "Permissions.Roles.View";
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const writes: { method: string; body: unknown }[] = [];
  await page.route("**/api/v1/identity/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (!path.includes("/roles") && !path.includes(`/${role.id}/permissions`)) return route.fallback();
    expect(request.headers().tenant).toBe("root");
    if (request.method() !== "GET") writes.push({ method: request.method(), body: request.postData() ? request.postDataJSON() : null });
    await route.fulfill({ json: path.endsWith("/roles") && request.method() === "GET" ? paged([role]) : role });
  });
  return writes;
}

test("read-only role viewer has no create, profile, grant or delete action", async ({ page }) => {
  const writes = await setup(page, [view]);
  await page.goto("/roles");
  await expect(page.getByRole("heading", { name: "Roles", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "New role" })).toHaveCount(0);
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByLabel(/^Name/)).toBeDisabled();
  await expect(page.getByLabel(/^Description/)).toBeDisabled();
  await expect(page.getByRole("checkbox").first()).toBeDisabled();
  await expect(page.getByRole("button", { name: /Save profile|Save permissions|Delete role/ })).toHaveCount(0);
  expect(writes).toEqual([]);
});

test("no view permission blocks list, detail and old create URL without role requests", async ({ page }) => {
  await setup(page, ["Permissions.Roles.Create", "Permissions.Roles.Update", "Permissions.Roles.Delete"]);
  const requests: string[] = [];
  page.on("request", request => { if (/identity\/(roles|role-manager)/.test(request.url())) requests.push(request.url()); });
  for (const path of ["/roles", `/roles/${role.id}`, "/roles/new"]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  }
  expect(requests).toEqual([]);
});

test("Create permits profile upsert but does not permit grants or deletion", async ({ page }) => {
  const writes = await setup(page, [view, "Permissions.Roles.Create"]);
  await page.goto(`/roles/${role.id}`);
  await page.getByLabel(/^Description/).fill("Updated operations");
  await page.getByRole("button", { name: "Save profile", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0]).toEqual({ method: "POST", body: { id: role.id, name: role.name, description: "Updated operations" } });
  await expect(page.getByRole("checkbox").first()).toBeDisabled();
  await expect(page.getByRole("button", { name: "Delete role", exact: true })).toHaveCount(0);
});

test("Update permits grants only and preserves assigned permissions absent from the catalog", async ({ page }) => {
  const writes = await setup(page, [view, "Permissions.Roles.Update"]);
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByLabel(/^Name/)).toBeDisabled();
  await page.getByText("Create users", { exact: true }).click();
  await page.getByRole("button", { name: "Save permissions", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0]).toEqual({ method: "PUT", body: { roleId: role.id, permissions: [...role.permissions, "Permissions.Users.Create"] } });
  await expect(page.getByRole("button", { name: "Delete role", exact: true })).toHaveCount(0);
});

test("Delete permits confirmed deletion only", async ({ page }) => {
  const writes = await setup(page, [view, "Permissions.Roles.Delete"]);
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByRole("button", { name: "Delete role", exact: true })).toBeDisabled();
  await page.getByPlaceholder(role.name, { exact: true }).fill(role.name);
  await page.getByRole("button", { name: "Delete role", exact: true }).click();
  await expect(page).toHaveURL(/\/roles$/);
  expect(writes).toEqual([{ method: "DELETE", body: null }]);
});

for (const name of ["Admin", "Basic"]) test(`${name} is fully read-only even with all role write permissions`, async ({ page }) => {
  const writes = await setup(page, [view, "Permissions.Roles.Create", "Permissions.Roles.Update", "Permissions.Roles.Delete"]);
  await page.route(`**/identity/${role.id}/permissions`, route => route.fulfill({ json: { ...role, name } }));
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByLabel(/^Name/)).toBeDisabled();
  await expect(page.getByLabel(/^Description/)).toBeDisabled();
  await expect(page.getByRole("checkbox").first()).toBeDisabled();
  await expect(page.getByRole("button", { name: /Save profile|Save permissions|Delete role/ })).toHaveCount(0);
  expect(writes).toEqual([]);
});

for (const status of [403, 500]) test(`detail ${status} is retryable and cannot expose writes without data`, async ({ page }) => {
  await setup(page, [view]);
  let failing = true;
  await page.route(`**/identity/${role.id}/permissions`, route => route.fulfill(failing
    ? { status, json: { detail: "Role request failed" } }
    : { json: role }));
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByText("Role request failed")).toBeVisible();
  await expect(page.getByRole("button", { name: /Save profile|Save permissions|Delete role/ })).toHaveCount(0);
  failing = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByLabel(/^Name/)).toHaveValue(role.name);
});

test("Chinese mobile read-only profile remains within the viewport", async ({ page }) => {
  await setup(page, [view]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto(`/roles/${role.id}`);
  await expect(page.getByText("只读：编辑资料需要角色创建权限，且仅支持自定义角色。")).toBeVisible();
  await expect(page.getByRole("checkbox").first()).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("read-only empty list does not offer a create action", async ({ page }) => {
  await setup(page, [view]);
  await page.route("**/identity/roles{,?*}", route => route.fulfill({ json: paged([]) }));
  await page.goto("/roles");
  await expect(page.getByText("No roles defined yet.", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "New role" })).toHaveCount(0);
});

test("list failure can be retried", async ({ page }) => {
  await setup(page, [view]);
  let failing = true;
  await page.route("**/identity/roles{,?*}", route => route.fulfill(failing
    ? { status: 403, json: { detail: "Role list forbidden" } }
    : { json: paged([role]) }));
  await page.goto("/roles");
  await expect(page.getByText("Role list forbidden")).toBeVisible();
  failing = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Manager\b/ })).toBeVisible();
});

test("profile write failure preserves inputs for explicit retry", async ({ page }) => {
  await setup(page, [view, "Permissions.Roles.Create"]);
  let failing = true;
  let attempts = 0;
  await page.route("**/identity/roles{,?*}", async route => {
    if (route.request().method() !== "POST") return route.fallback();
    attempts++;
    expect(route.request().headers().tenant).toBe("root");
    await route.fulfill(failing
      ? { status: 403, json: { detail: "Profile update forbidden" } }
      : { json: role });
  });
  await page.goto(`/roles/${role.id}`);
  await page.getByLabel(/^Description/).fill("Retain on failure");
  await page.getByRole("button", { name: "Save profile", exact: true }).click();
  await expect(page.getByText("Profile update forbidden")).toBeVisible();
  await expect(page.getByLabel(/^Description/)).toHaveValue("Retain on failure");
  expect(attempts).toBe(1);
  failing = false;
  await page.getByRole("button", { name: "Save profile", exact: true }).click();
  await expect.poll(() => attempts).toBe(2);
});
