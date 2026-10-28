import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

const CV = "Permissions.Ordering.Customers.View";
const CU = "Permissions.Ordering.Customers.Update";
const PLV = "Permissions.Catalog.PriceLists.View";
const PLC = "Permissions.Catalog.PriceLists.Create";
const PLU = "Permissions.Catalog.PriceLists.Update";
const A = "11111111-1111-1111-1111-111111111111";
const B = "22222222-2222-2222-2222-222222222222";
const VIP = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const GOLD = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const customers = [
  { id: A, code: "ACME", name: "Acme Restaurant", customerTenantId: "ACME", creditHold: false },
  { id: B, code: "BETA", name: "Beta Restaurant", customerTenantId: "BETA", creditHold: false },
];
const levels = [
  { id: VIP, code: "VIP", name: "VIP", discountPercent: 10, customerCount: 1 },
  { id: GOLD, code: "GOLD", name: "Gold", discountPercent: 0, customerCount: 0 },
];

async function setup(page: Page, perms: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions: perms });
  await installAdminShellMocks(page, perms);
  await mockJsonResponse(page, "**/api/v1/ordering/customer-orgs**", customers, { method: "GET" });
  await page.route("**/api/v1/catalog/customer-levels/assignments", route =>
    route.request().method() === "GET" ? route.fulfill({ json: [{ customerOrgId: A, customerLevelId: VIP }] }) : route.fallback());
  await page.route("**/api/v1/catalog/customer-levels", route =>
    route.request().method() === "GET" ? route.fulfill({ json: levels }) : route.fallback());
  await mockJsonResponse(page, "**/api/v1/catalog/price-lists**", [
    { id: "pl-1", name: "VIP specials", customerOrgId: null, customerLevelId: VIP, priority: 1, validFrom: "2026-09-01T00:00:00Z", validTo: null, lines: [] },
  ], { method: "GET" });
  await mockJsonResponse(page, "**/api/v1/catalog/products**", paged([]), { method: "GET" });
}

function card(page: Page, name: string) {
  return page.getByRole("article").filter({ has: page.getByRole("heading", { name }) });
}

test("customer cards show their level and the editor moves a customer to another level", async ({ page }) => {
  await setup(page, [CV, CU, PLV, PLU]);
  const writes: { url: string; body: unknown }[] = [];
  await page.route("**/api/v1/ordering/customer-orgs/details", async route => { writes.push({ url: route.request().url(), body: route.request().postDataJSON() }); await route.fulfill({ status: 204 }); });
  await page.route(`**/api/v1/catalog/customer-levels/assignments/${A}`, async route => { writes.push({ url: route.request().url(), body: route.request().postDataJSON() }); await route.fulfill({ status: 204 }); });

  await page.goto("/customers");
  await expect(card(page, "Acme Restaurant")).toContainText("VIP · Pay 90%");
  await expect(card(page, "Beta Restaurant")).toContainText("Standard price (no level)");

  await card(page, "Acme Restaurant").getByRole("button", { name: "Edit customer" }).click();
  const level = card(page, "Acme Restaurant").getByLabel("Customer level");
  await expect(level).toHaveValue(VIP);
  await level.selectOption(GOLD);
  await card(page, "Acme Restaurant").getByRole("button", { name: "Save changes" }).click();

  await expect.poll(() => writes.length).toBe(2);
  expect(writes[0].body).toMatchObject({ customerOrgId: A, code: "ACME", name: "Acme Restaurant" });
  expect(writes[1].body).toEqual({ customerLevelId: GOLD });
});

test("saving a customer without changing its level does not touch the level", async ({ page }) => {
  await setup(page, [CV, CU, PLV, PLU]);
  let assigned = false;
  await page.route("**/api/v1/ordering/customer-orgs/details", route => route.fulfill({ status: 204 }));
  await page.route("**/api/v1/catalog/customer-levels/assignments/*", route => { assigned = true; return route.fulfill({ status: 204 }); });

  await page.goto("/customers");
  await card(page, "Beta Restaurant").getByRole("button", { name: "Edit customer" }).click();
  await card(page, "Beta Restaurant").getByRole("button", { name: "Save changes" }).click();
  await expect(card(page, "Beta Restaurant").getByRole("button", { name: "Edit customer" })).toBeVisible();
  expect(assigned).toBe(false);
});

test("pricing tab merges levels with contract pricing and creates a level", async ({ page }) => {
  await setup(page, [CV, PLV, PLC, PLU]);
  let created: unknown;
  await page.route("**/api/v1/catalog/customer-levels", async route => {
    if (route.request().method() !== "POST") return route.fallback();
    created = route.request().postDataJSON();
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    await route.fulfill({ json: "new-level" });
  });

  await page.goto("/customers");
  await page.getByRole("tab", { name: "Levels and contract prices" }).click();
  await expect(page).toHaveURL(/tab=pricing/);
  await expect(page.getByRole("heading", { name: "Customer levels" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Pay 90%" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "VIP specials" })).toBeVisible();
  await expect(page.getByText("Level · VIP")).toBeVisible();

  await page.getByRole("button", { name: "New level" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Level code").fill("silver");
  await dialog.getByLabel("Level name").fill("Silver");
  await expect(dialog.getByLabel("Discount (%)")).toHaveValue("100");
  await dialog.getByLabel("Discount (%)").fill("95");
  await dialog.getByRole("button", { name: "Save level" }).click();
  await expect(dialog).toHaveCount(0);
  expect(created).toEqual({ code: "silver", name: "Silver", discountPercent: 5 });
});

test("old contract pricing link opens the pricing tab and the menu no longer lists it", async ({ page }) => {
  await setup(page, [PLV]);
  const requests: string[] = [];
  page.on("request", r => requests.push(r.url()));
  await page.goto("/catalog/pricing");
  await expect(page).toHaveURL(/\/customers\?tab=pricing/);
  await expect(page.getByRole("heading", { name: "Customer levels" })).toBeVisible();
  // 只有定价查看权限：没有页签、没有写按钮，也不请求客户列表
  await expect(page.getByRole("tab")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "New level" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Contract pricing" })).toHaveCount(0);
  expect(requests.some(url => url.includes("/ordering/customer-orgs"))).toBe(false);
});
