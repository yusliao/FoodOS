import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

const CV = "Permissions.Ordering.Customers.View";
const CC = "Permissions.Ordering.Customers.Create";
const SV = "Permissions.Ordering.Stores.View";
const SC = "Permissions.Ordering.Stores.Create";
const WV = "Permissions.Inventory.Warehouses.View";
const A = "11111111-1111-1111-1111-111111111111";
const B = "22222222-2222-2222-2222-222222222222";
const W = "33333333-3333-3333-3333-333333333333";
const customers = [
  { id: A, code: "ACME", name: "Acme Restaurant", customerTenantId: "ACME", creditHold: false },
  { id: B, code: "BETA", name: "Beta Restaurant", customerTenantId: "BETA", creditHold: true },
];
const store = { id: "s-1", customerOrgId: A, customerTenantId: "ACME", code: "A1", name: "Acme Main",
  address: "123 Market Street", defaultWarehouseId: W, defaultRouteId: null, deliveryWindow: "05:00–08:00" };

async function setup(page: Page, perms: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions: perms });
  await installAdminShellMocks(page, perms);
  await mockJsonResponse(page, "**/api/v1/ordering/customer-orgs**", customers, { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/ordering/stores**", [store], { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/inventory/warehouses**", paged([{ id: W, code: "WH1", name: "Main warehouse" }]));
}

test("read-only customers show business records, not tenant management or write actions", async ({ page }, info) => {
  await setup(page, [CV]);
  const requests: string[] = [];
  page.on("request", r => requests.push(r.url()));
  await page.goto("/customers");
  await expect(page.getByRole("heading", { name: "Acme Restaurant" })).toBeVisible();
  await expect(page.getByRole("button", { name: "New customer" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "View stores" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Customer identity domains" })).toHaveCount(0);
  expect(requests.some(url => /\/api\/v1\/(tenants|multitenancy|inventory)/.test(url))).toBe(false);
  await page.screenshot({ path: info.outputPath("customers-desktop.png"), fullPage: true });
});

test("customer search preserves root identity and customer link filters stores", async ({ page }) => {
  await setup(page, [CV, SV]);
  await page.goto("/customers");
  await page.getByRole("searchbox", { name: "Search customers" }).fill("Acme");
  const searched = page.waitForRequest(r => r.url().includes("customer-orgs?search=Acme"));
  await page.getByRole("button", { name: "Search customers" }).click();
  expect((await searched).headers().tenant).toBe("root");
  const filtered = page.waitForRequest(r => r.url().includes(`stores?customerOrgId=${A}`));
  await page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Acme Restaurant" }) }).getByRole("link", { name: "View stores" }).click();
  expect((await filtered).headers().tenant).toBe("root");
  await expect(page.getByRole("heading", { name: "Acme Main" })).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`customerOrgId=${A}`));
});

for (const path of ["customers", "stores"]) {
  test(`no permission prevents ${path} API calls and direct route access`, async ({ page }) => {
    await setup(page, []);
    const requests: string[] = [];
    page.on("request", r => requests.push(r.url()));
    await page.goto(`/${path}`);
    await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
    await expect(page.getByRole("main")).toContainText(path === "customers" ? CV : SV);
    expect(requests.some(url => url.includes("/ordering/"))).toBe(false);
  });
}

test("customer creation rejects root, keeps error input and reuses the key for an unchanged retry", async ({ page }) => {
  await setup(page, [CV, CC]);
  const calls: { data: Record<string, unknown>; key: string; tenant: string }[] = [];
  await page.route("**/api/v1/ordering/customer-orgs", async route => {
    calls.push({ data: route.request().postDataJSON(), key: route.request().headers()["idempotency-key"], tenant: route.request().headers().tenant });
    if (calls.length === 1) await route.fulfill({ status: 409, contentType: "application/problem+json", body: JSON.stringify({ detail: "Customer already linked" }) });
    else await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(A) });
  });
  await page.goto("/customers");
  await page.getByRole("button", { name: "New customer" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Customer identity domain").fill("root");
  await dialog.getByRole("textbox", { name: "Code", exact: true }).fill("NEW");
  await dialog.getByRole("textbox", { name: "Name", exact: true }).fill("New Restaurant");
  await expect(dialog.getByRole("button", { name: "Create", exact: true })).toBeDisabled();
  await expect(dialog.getByRole("alert")).toContainText("root belongs to the operator");
  await dialog.getByLabel("Customer identity domain").fill("new-restaurant");
  await dialog.getByLabel("Create with credit hold").check();
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Customer already linked");
  await expect(dialog.getByRole("textbox", { name: "Name", exact: true })).toHaveValue("New Restaurant");
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(calls).toHaveLength(2);
  expect(calls[0].data).toEqual({ customerTenantId: "new-restaurant", code: "NEW", name: "New Restaurant", creditHold: true });
  expect(calls[0].tenant).toBe("root");
  expect(calls[0].key).toBeTruthy();
  expect(calls[1].key).toBe(calls[0].key);
});

test("changing a failed customer payload starts a new idempotency key", async ({ page }) => {
  await setup(page, [CV, CC]);
  const keys: string[] = [];
  await page.route("**/api/v1/ordering/customer-orgs", async route => {
    keys.push(route.request().headers()["idempotency-key"]);
    await route.fulfill({ status: 409, contentType: "application/problem+json", body: JSON.stringify({ detail: "Duplicate code" }) });
  });
  await page.goto("/customers");
  await page.getByRole("button", { name: "New customer" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Customer identity domain").fill("acme");
  await dialog.getByRole("textbox", { name: "Code", exact: true }).fill("OLD");
  await dialog.getByRole("textbox", { name: "Name", exact: true }).fill("A");
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Duplicate code");
  await dialog.getByRole("textbox", { name: "Code", exact: true }).fill("NEW");
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect.poll(() => keys.length).toBe(2);
  expect(keys[1]).not.toBe(keys[0]);
});

test("stores-only reader does not fetch customers, warehouses or identity domains", async ({ page }) => {
  await setup(page, [SV]);
  const requests: string[] = [];
  page.on("request", r => requests.push(r.url()));
  await page.goto("/stores");
  await expect(page.getByRole("heading", { name: "Acme Main" })).toBeVisible();
  await expect(page.getByRole("button", { name: "New store" })).toHaveCount(0);
  expect(requests.some(url => /customer-orgs|inventory\/warehouses|\/tenants/.test(url))).toBe(false);
});

test("store creator without lookup permissions gets guidance and no forbidden lookups", async ({ page }) => {
  await setup(page, [SV, SC]);
  const requests: string[] = [];
  page.on("request", r => requests.push(r.url()));
  await page.goto("/stores");
  await expect(page.getByRole("button", { name: "New store" })).toBeDisabled();
  await expect(page.getByText(/Creating a store requires customer-view/)).toBeVisible();
  expect(requests.some(url => /customer-orgs|inventory\/warehouses/.test(url))).toBe(false);
});

for (const id of ["invalid-id", "00000000-0000-0000-0000-000000000000"]) {
  test(`invalid filter ${id} never silently requests all stores`, async ({ page }) => {
    await setup(page, [SV]);
    const requests: string[] = [];
    page.on("request", r => requests.push(r.url()));
    await page.goto(`/stores?customerOrgId=${id}`);
    await expect(page.getByRole("alert")).toContainText("Invalid customer ID");
    expect(requests.some(url => url.includes("/ordering/stores"))).toBe(false);
    await page.getByRole("button", { name: "Clear filter" }).click();
    await expect(page.getByRole("heading", { name: "Acme Main" })).toBeVisible();
  });
}

test("store creation uses the current customer and paged warehouse, refreshes the list", async ({ page }) => {
  await setup(page, [CV, SV, SC, WV]);
  const posts: Record<string, unknown>[] = [];
  let storeGets = 0;
  await page.route("**/api/v1/ordering/stores**", async route => {
    if (route.request().method() === "POST") {
      expect(route.request().headers().tenant).toBe("root");
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      posts.push(route.request().postDataJSON());
      await route.fulfill({ json: "created" });
    } else { storeGets++; await route.fulfill({ json: [store] }); }
  });
  await page.route("**/api/v1/inventory/warehouses**", async route => {
    const p = Number(new URL(route.request().url()).searchParams.get("pageNumber"));
    await route.fulfill({ json: paged(p === 1 ? [{ id: A, code: "OLD", name: "Old warehouse" }] : [{ id: W, code: "WH2", name: "Second warehouse" }], { pageNumber: p, totalCount: 21, totalPages: 2 }) });
  });
  await page.goto(`/stores?customerOrgId=${A}`);
  await page.getByRole("button", { name: "New store" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "Partner customer", exact: true }).click();
  await page.getByRole("menuitem", { name: /BETA/ }).click();
  await dialog.getByRole("textbox", { name: "Code", exact: true }).fill("B1");
  await dialog.getByRole("textbox", { name: "Name", exact: true }).fill("Beta Main");
  await dialog.getByRole("textbox", { name: "Address", exact: true }).fill("456 Main Street");
  await dialog.getByLabel("Delivery window", { exact: true }).fill("06:00–08:00");
  await dialog.getByRole("button", { name: "Next", exact: true }).click();
  await dialog.getByRole("button", { name: "WH2 · Second warehouse" }).click();
  expect(posts).toHaveLength(0);
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(posts).toEqual([{ customerOrgId: B, code: "B1", name: "Beta Main", address: "456 Main Street", defaultWarehouseId: W, defaultRouteId: null, deliveryWindow: "06:00–08:00" }]);
  await expect.poll(() => storeGets).toBeGreaterThan(1);
});

test("failed customer listing is not shown as empty and can be retried", async ({ page }) => {
  await setup(page, [CV]);
  let fail = true;
  await page.route("**/api/v1/ordering/customer-orgs**", async route => {
    await route.fulfill(fail ? { status: 403, json: { detail: "Permission removed" } } : { json: [] });
  });
  await page.goto("/customers");
  await expect(page.getByText("Permission removed")).toBeVisible();
  await expect(page.getByText("No partner customers match this search.")).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No partner customers match this search.")).toBeVisible();
});

test("warehouse lookup failure blocks creation until retry succeeds; server rejection preserves the form", async ({ page }) => {
  await setup(page, [CV, SV, SC, WV]);
  let failed = true;
  await page.route("**/api/v1/inventory/warehouses**", async route => {
    await route.fulfill(failed ? { status: 500, json: { detail: "Warehouse lookup unavailable" } }
      : { json: paged([{ id: W, code: "WH1", name: "Main warehouse" }]) });
  });
  await page.route("**/api/v1/ordering/stores", async route => {
    await route.fulfill({ status: 403, json: { detail: "Create permission revoked" } });
  });
  await page.goto(`/stores?customerOrgId=${A}`);
  await page.getByRole("button", { name: "New store" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("textbox", { name: "Code", exact: true }).fill("A2");
  await dialog.getByRole("textbox", { name: "Name", exact: true }).fill("Acme Second");
  await dialog.getByRole("textbox", { name: "Address", exact: true }).fill("Address");
  // Allow the application's existing transient-error retry/backoff to finish.
  await expect(dialog.getByRole("alert")).toContainText("Warehouse lookup unavailable", { timeout: 15_000 });
  await expect(dialog.getByRole("button", { name: "Create", exact: true })).toBeDisabled();
  failed = false;
  await dialog.getByRole("button", { name: "Retry", exact: true }).click();
  await dialog.getByRole("button", { name: "WH1 · Main warehouse" }).click();
  await dialog.getByRole("button", { name: "Create", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Create permission revoked");
  await expect(dialog.getByRole("textbox", { name: "Name", exact: true })).toHaveValue("Acme Second");
  await expect(page.getByText("Store created for the selected customer. Your list filter is unchanged.")).toHaveCount(0);
});

test("unlinked or operator-owned legacy customers cannot be selected for a new restaurant store", async ({ page }, info) => {
  await setup(page, [CV, SV, SC, WV]);
  await mockJsonResponse(page, "**/api/v1/ordering/customer-orgs**", [
    ...customers,
    { id: W, code: "OLD", name: "Unlinked customer", customerTenantId: null },
    { id: "root-org", code: "ROOT", name: "Operator record", customerTenantId: "ROOT" },
  ]);
  await page.goto("/stores");
  await page.getByRole("button", { name: "New store" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "Partner customer", exact: true }).click();
  await expect(page.getByRole("menuitem", { name: /Unlinked customer|Operator record/ })).toHaveCount(0);
  await page.getByRole("menuitem", { name: /ACME/ }).click();
  await expect(dialog.getByRole("button", { name: "WH1 · Main warehouse" })).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Create", exact: true })).toBeDisabled();
  await page.screenshot({ path: info.outputPath("store-create.png"), fullPage: true });
});

test("Chinese mobile customer and store pages fit the viewport", async ({ page }, info) => {
  await setup(page, [CV, SV]);
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/customers");
  await expect(page.getByRole("heading", { name: "合作客户", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Acme Restaurant" })).toBeVisible();
  await page.screenshot({ path: info.outputPath("customers-mobile.png"), fullPage: true });
  await page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Acme Restaurant" }) }).getByRole("link", { name: "查看门店" }).click();
  await expect(page.getByRole("heading", { name: "Acme Main" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath("stores-mobile.png"), fullPage: true });
});
