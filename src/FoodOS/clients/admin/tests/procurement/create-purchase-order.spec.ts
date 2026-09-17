import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const permissions = ["Permissions.Procurement.Purchase.View", "Permissions.Procurement.Purchase.Create", "Permissions.Procurement.Suppliers.View", "Permissions.Inventory.Warehouses.View", "Permissions.Catalog.Products.View"];
async function setup(page: Page, grants = permissions) {
  await seedAuthedSession(page, { ...TEST_USER, permissions: grants });
  await installAdminShellMocks(page, grants);
  await page.route("**/api/v1/fulfillment/capabilities", route => route.fulfill({ json: { mode: "externalWms", readiness: "notConfigured", acceptsOrders: false, acceptsOrderChanges: false, localWarehouseExecution: false } }));
  await page.route("**/api/v1/procurement/purchase-orders?**", route => route.fulfill({ json: [] }));
  await page.route("**/api/v1/procurement/suppliers?**", route => route.fulfill({ json: [{ id: "supplier-1", code: "SUP", name: "Supplier One" }] }));
  for (const [path, prefix] of [["inventory/warehouses", "warehouse"], ["catalog/products", "product"]]) {
    await page.route("**/api/v1/" + path + "?**", route => {
      const url = new URL(route.request().url());
      const pageNumber = Number(url.searchParams.get("pageNumber"));
      return route.fulfill({ json: { items: [{ id: prefix + "-" + pageNumber, code: "W" + pageNumber, sku: "P" + pageNumber, name: prefix + " " + pageNumber }], pageNumber, totalCount: 2, totalPages: 2, hasNext: pageNumber === 1, hasPrevious: pageNumber === 2 } });
    });
  }
}
async function openAndFill(page: Page) {
  await page.goto("/procurement/purchase-orders");
  await page.getByRole("button", { name: "Create purchase order" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "SUP · Supplier One" }).click();
  await dialog.getByRole("button", { name: "W1 · warehouse 1" }).click();
  await dialog.getByLabel("Expected arrival").fill("2026-09-20T10:30");
  await dialog.getByRole("button", { name: "P1 · product 1" }).click();
  return dialog;
}

for (const missing of permissions.slice(2)) {
  test("missing lookup permission disables creation without lookup requests: " + missing, async ({ page }) => {
    await setup(page, permissions.filter(value => value !== missing));
    const requests: string[] = [];
    page.on("request", request => requests.push(request.url()));
    await page.goto("/procurement/purchase-orders");
    await expect(page.getByRole("button", { name: "Create purchase order" })).toBeDisabled();
    await expect(page.getByText("Creating a purchase order also requires")).toBeVisible();
    expect(requests.some(url => /api\/v1\/(catalog|inventory)|api\/v1\/procurement\/suppliers/.test(url))).toBe(false);
  });
}

test("multi-line creation retains paged selections, temperature zones and root identity", async ({ page }) => {
  await setup(page);
  let body: { supplierId: string; warehouseId: string; expectedAt: string; lines: unknown[] } | undefined;
  let writes = 0;
  await page.route("**/api/v1/procurement/purchase-orders", async route => {
    writes++;
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    body = route.request().postDataJSON();
    await route.fulfill({ json: "created-po" });
  });
  const dialog = await openAndFill(page);
  await dialog.getByRole("region", { name: "Warehouse", exact: true }).getByRole("button", { name: "Next" }).click();
  await expect(dialog.getByText("Selected: W1 · warehouse 1")).toBeVisible();
  await dialog.getByRole("button", { name: "W2 · warehouse 2" }).click();
  await dialog.getByRole("region", { name: "Add product", exact: true }).getByRole("button", { name: "Next" }).click();
  await dialog.getByRole("button", { name: "P2 · product 2" }).click();
  const line = dialog.getByRole("region", { name: "Purchase line 2", exact: true });
  await line.getByLabel("Ordered quantity").fill("2.5");
  await line.getByLabel("Temperature zone").click();
  await page.getByRole("menuitem", { name: "Frozen" }).click();
  expect(writes).toBe(0);
  await dialog.getByRole("button", { name: "Create purchase order" }).click();
  await expect(dialog).toHaveCount(0);
  expect(writes).toBe(1);
  expect(body?.supplierId).toBe("supplier-1");
  expect(body?.warehouseId).toBe("warehouse-2");
  expect(body?.expectedAt).toBe(await page.evaluate(() => new Date("2026-09-20T10:30").toISOString()));
  expect(body?.lines).toEqual([{ productId: "product-1", zone: "Ambient", quantity: 1 }, { productId: "product-2", zone: "Frozen", quantity: 2.5 }]);
});

test("creation failures preserve input and reuse keys only for unchanged payloads", async ({ page }) => {
  await setup(page);
  const keys: string[] = [];
  await page.route("**/api/v1/procurement/purchase-orders", async route => {
    keys.push(route.request().headers()["idempotency-key"]);
    await route.fulfill(keys.length < 3 ? { status: keys.length === 1 ? 403 : 409, json: { detail: "Create rejected" } } : { json: "created-po" });
  });
  const dialog = await openAndFill(page);
  const save = dialog.getByRole("button", { name: "Create purchase order" });
  await save.click();
  await expect(dialog.getByText("Create rejected")).toBeVisible();
  await expect(dialog.getByLabel("Expected arrival")).toHaveValue("2026-09-20T10:30");
  await save.click();
  await expect.poll(() => keys.length).toBe(2);
  await expect(save).toBeEnabled();
  await dialog.getByLabel("Ordered quantity").fill("4");
  await save.click();
  await expect(dialog).toHaveCount(0);
  expect(keys[0]).toBe(keys[1]);
  expect(keys[2]).not.toBe(keys[1]);
});

test("lookup failure retries to empty and prevents incomplete creation", async ({ page }) => {
  await setup(page);
  let denied = true;
  await page.route("**/api/v1/procurement/suppliers?**", route => route.fulfill(denied ? { status: 403, json: { detail: "Supplier lookup denied" } } : { json: [] }));
  await page.goto("/procurement/purchase-orders");
  await page.getByRole("button", { name: "Create purchase order" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("Supplier lookup denied")).toBeVisible();
  denied = false;
  await dialog.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(dialog.getByText("No matching options.")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Create purchase order" })).toBeDisabled();
});

test("search resets lookup pagination and removing the last line disables saving", async ({ page }) => {
  await setup(page);
  const requests: URL[] = [];
  page.on("request", request => {
    if (/api\/v1\/(catalog\/products|inventory\/warehouses|procurement\/suppliers)/.test(request.url())) {
      expect(request.headers().tenant).toBe("root");
      requests.push(new URL(request.url()));
    }
  });
  const dialog = await openAndFill(page);
  const productLookup = dialog.getByRole("region", { name: "Add product", exact: true });
  await productLookup.getByRole("button", { name: "Next" }).click();
  await expect(dialog.getByRole("button", { name: "P2 · product 2" })).toBeVisible();
  await productLookup.getByLabel("Add product").fill("apple");
  await expect.poll(() => requests.some(url => url.pathname.endsWith("/products") && url.searchParams.get("search") === "apple" && url.searchParams.get("pageNumber") === "1")).toBe(true);
  await dialog.getByLabel("Ordered quantity").fill("0");
  await expect(dialog.getByRole("button", { name: "Create purchase order" })).toBeDisabled();
  await dialog.getByRole("button", { name: "Remove line" }).click();
  await expect(dialog.getByText("Choose at least one product.")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Create purchase order" })).toBeDisabled();
});

test("Chinese mobile creation has usable labels and no horizontal overflow", async ({ page }) => {
  await setup(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/procurement/purchase-orders");
  await page.getByRole("button", { name: "新建采购单" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("预计到货")).toBeVisible();
  await dialog.getByRole("button", { name: "P1 · product 1" }).click();
  await expect(dialog.getByText("移除明细")).toBeVisible();
  expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
});
