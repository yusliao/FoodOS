import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const view = "Permissions.Procurement.Suppliers.View";
const create = "Permissions.Procurement.Suppliers.Create";
const supplier = { id: "supplier-1", code: "FRESH", name: "Fresh Foods", categories: "Produce", leadDays: 2, status: "Active", createdAtUtc: "2026-09-17T00:00:00Z" };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/procurement/suppliers**", route => route.fulfill({ json: [supplier] }));
}

test("read-only supplier navigation and search keep root identity without other lookups", async ({ page }) => {
  await setup(page, [view]);
  const requests: string[] = [];
  page.on("request", request => requests.push(request.url()));
  await page.goto("/");
  await page.getByRole("main").getByRole("link", { name: "Suppliers", exact: true }).click();
  await expect(page.getByRole("heading", { name: supplier.name })).toBeVisible();
  await expect(page.getByRole("button", { name: "New supplier" })).toHaveCount(0);
  const request = page.waitForRequest(request => request.url().includes("suppliers?search=Fresh"));
  await page.getByLabel("Search supplier code or name").fill("Fresh");
  expect((await request).headers().tenant).toBe("root");
  expect(requests.some(url => /api\/v1\/(catalog|inventory)|procurement\/purchase-orders/.test(url))).toBe(false);
});

test("direct supplier URL requires view even when create is granted", async ({ page }) => {
  await setup(page, [create]);
  let requests = 0;
  page.on("request", request => { if (request.url().includes("/api/v1/procurement")) requests++; });
  await page.goto("/procurement/suppliers");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toBe(0);
});

test("supplier 403 can retry into an explicit empty state", async ({ page }) => {
  await setup(page, [view]);
  let fails = true;
  await page.route("**/api/v1/procurement/suppliers**", route => route.fulfill(fails ? { status: 403, json: { detail: "Supplier access denied" } } : { json: [] }));
  await page.goto("/procurement/suppliers");
  await expect(page.getByText("Supplier access denied")).toBeVisible();
  await expect(page.getByText("No matching suppliers.")).toHaveCount(0);
  fails = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No matching suppliers.")).toBeVisible();
});

test("supplier create preserves failed values and key, changed payload gets a fresh key", async ({ page }) => {
  await setup(page, [view, create]);
  const writes: { key: string; body: Record<string, unknown> }[] = [];
  await page.route("**/api/v1/procurement/suppliers", async route => {
    expect(route.request().headers().tenant).toBe("root");
    writes.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
    await route.fulfill(writes.length < 3 ? { status: 409, json: { detail: "Supplier code already exists" } } : { json: "supplier-2" });
  });
  await page.goto("/procurement/suppliers");
  await page.getByRole("button", { name: "New supplier" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Supplier code").fill("NEW");
  await dialog.getByLabel("Supplier name").fill("New Foods");
  await dialog.getByLabel("Lead time (days)").fill("-1");
  await expect(dialog.getByRole("button", { name: "Create supplier" })).toBeDisabled();
  await dialog.getByLabel("Lead time (days)").fill("3");
  await dialog.getByRole("button", { name: "Create supplier" }).click();
  await expect(dialog.getByText("Supplier code already exists")).toBeVisible();
  await expect(dialog.getByLabel("Supplier name")).toHaveValue("New Foods");
  await dialog.getByRole("button", { name: "Create supplier" }).click();
  await expect.poll(() => writes.length).toBe(2);
  await dialog.getByLabel("Supplier code").fill("NEW2");
  await dialog.getByRole("button", { name: "Create supplier" }).click();
  await expect(dialog).toHaveCount(0);
  expect(writes[0].key).toBeTruthy();
  expect(writes[0]).toEqual(writes[1]);
  expect(writes[2].key).not.toBe(writes[0].key);
  expect(writes[2].body).toEqual({ code: "NEW2", name: "New Foods", categories: null, leadDays: 3 });
});

test("Chinese mobile supplier cards fit the viewport", async ({ page }) => {
  await setup(page, [view]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/procurement/suppliers");
  await expect(page.getByRole("heading", { name: "供应商管理" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Fresh Foods" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});
