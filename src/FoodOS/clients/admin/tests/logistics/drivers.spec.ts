import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
const view = "Permissions.Logistics.Drivers.View", create = "Permissions.Logistics.Drivers.Create", users = "Permissions.Users.View";
const driver = { id: "d1", userId: "11111111-1111-4111-8111-111111111111", phone: "555-0100" };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/logistics/drivers", route => route.fulfill({ json: [driver] }));
}
test("readonly navigation avoids employee queries", async ({ page }) => {
  await setup(page, [view]);
  let lookups = 0;
  page.on("request", request => { if (request.url().includes("/identity/users")) lookups++; });
  await page.goto("/");
  await page.getByRole("main").getByRole("link", { name: "Drivers", exact: true }).click();
  await expect(page.getByRole("heading", { name: driver.phone })).toBeVisible();
  await expect(page.getByRole("button", { name: "New driver" })).toHaveCount(0);
  expect(lookups).toBe(0);
});
test("create without lookup permission cannot query users", async ({ page }) => {
  await setup(page, [view, create]);
  let lookups = 0;
  page.on("request", request => { if (request.url().includes("/identity/users")) lookups++; });
  await page.goto("/logistics/drivers");
  await expect(page.getByRole("button", { name: "New driver" })).toBeDisabled();
  await expect(page.getByText("User viewing permission is required to select an operator account.")).toBeVisible();
  expect(lookups).toBe(0);
});
for (const permissions of [[], [create, users]]) test(`direct driver URL requires view ${permissions.length}`, async ({ page }) => {
  await setup(page, permissions);
  let reads = 0;
  page.on("request", request => { if (/\/api\/v1\/(logistics\/drivers|identity\/users)/.test(request.url())) reads++; });
  await page.goto("/logistics/drivers");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(reads).toBe(0);
});
test("list 403 retries to empty", async ({ page }) => {
  await setup(page, [view]);
  let fail = true;
  await page.route("**/api/v1/logistics/drivers", route => route.fulfill(fail ? { status: 403, json: { detail: "Driver denied" } } : { json: [] }));
  await page.goto("/logistics/drivers");
  await expect(page.getByText("Driver denied")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No drivers registered.")).toBeVisible();
});
test("paged active operator selection and failed registration retain data and key", async ({ page }) => {
  await setup(page, [view, create, users]);
  await page.route("**/api/v1/identity/users/search?**", route => {
    const url = new URL(route.request().url());
    expect(route.request().headers().tenant).toBe("root");
    expect(url.searchParams.get("IsActive")).toBe("true");
    const p = Number(url.searchParams.get("PageNumber"));
    return route.fulfill({ json: { items: [{ id: driver.userId, firstName: "Driver", lastName: String(p), userName: "driver" + p, isActive: true }], totalCount: 2, totalPages: 2, hasNext: p === 1, hasPrevious: p === 2 } });
  });
  const writes: { key: string; body: unknown }[] = [];
  await page.route("**/api/v1/logistics/drivers", route => {
    if (route.request().method() === "GET") return route.fulfill({ json: [driver] });
    expect(route.request().headers().tenant).toBe("root");
    writes.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
    return route.fulfill(writes.length < 3 ? { status: 400, json: { detail: "User no longer active" } } : { json: "d1" });
  });
  await page.goto("/logistics/drivers");
  await page.getByRole("button", { name: "New driver" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("button", { name: "Register driver" })).toBeDisabled();
  await dialog.getByRole("button", { name: "Next" }).click();
  await dialog.getByRole("button", { name: "Driver 2 · driver2" }).click();
  await dialog.getByLabel("Search active operator users").fill("Driver");
  await expect(dialog.getByRole("button", { name: "Driver 1 · driver1" })).toBeVisible();
  await expect(dialog.getByText(/Selected user: Driver 2/)).toBeVisible();
  await dialog.getByLabel("Driver phone").fill("555-0100");
  await dialog.getByRole("button", { name: "Register driver" }).click();
  await expect(dialog.getByText("User no longer active")).toBeVisible();
  await dialog.getByRole("button", { name: "Register driver" }).click();
  await expect.poll(() => writes.length).toBe(2);
  await dialog.getByLabel("Driver phone").fill("555-0101");
  await dialog.getByRole("button", { name: "Register driver" }).click();
  await expect(dialog).toHaveCount(0);
  expect(writes[0].key).toBeTruthy();
  expect(writes[0]).toEqual(writes[1]);
  expect(writes[2].key).not.toBe(writes[0].key);
  expect(writes[2].body).toEqual({ userId: driver.userId, phone: "555-0101" });
});
test("Chinese mobile lookup failure retries to empty without permitting registration", async ({ page }) => {
  await setup(page, [view, create, users]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  let fail = true;
  await page.route("**/api/v1/identity/users/search?**", route => route.fulfill(fail ? { status: 500, json: {} } : { json: { items: [], totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false } }));
  await page.goto("/logistics/drivers");
  await expect(page.getByRole("heading", { name: "司机档案" })).toBeVisible();
  await page.getByRole("button", { name: "新增司机" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("button", { name: "重试", exact: true })).toBeVisible();
  fail = false;
  await dialog.getByRole("button", { name: "重试", exact: true }).click();
  await expect(dialog.getByText("没有匹配的活跃用户。")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "登记司机" })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});
