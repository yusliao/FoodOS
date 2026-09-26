import { expect, test } from "@playwright/test";

const dashboardUrl = process.env.PLAYWRIGHT_DEMO_DASHBOARD_URL ?? "http://localhost:19082";

test("operator demo picker signs a purchaser into the deployed workbench", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByRole("img", { name: "东方味力" }).first()).toBeVisible();
  await expect(page.getByText("东方味力").first()).toBeVisible();
  await page.getByRole("button", { name: /sign in with a demo account/i }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("purchaser@root.com")).toBeVisible();
  await dialog.getByRole("button", { name: /Purchaser/ }).click();
  await expect(page).toHaveURL(/\/$/);

  await page.goto("/procurement/purchase-orders");
  await expect(page.getByRole("heading", { name: "Purchase orders", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Create purchase order", exact: true })).toBeEnabled();
});

test("restaurant demo picker signs Acme in and places an order through the demo WMS", async ({ page, request }) => {
  test.setTimeout(120_000);
  await request.post("http://127.0.0.1:19083/demo/mode", { data: { mode: "accepted" } });
  await page.goto(`${dashboardUrl}/login`);
  await expect(page.getByRole("img", { name: "东方味力" })).toBeVisible();
  await expect(page.getByText("东方味力").first()).toBeVisible();
  await page.getByRole("button", { name: /sign in with a demo account/i }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("admin@acme.com")).toBeVisible();
  await dialog.getByRole("button", { name: /admin@acme\.com/i }).click();
  await expect(page).not.toHaveURL(/\/login$/);

  await page.goto(`${dashboardUrl}/shop/catalog`);
  await expect(page.getByRole("heading", { name: /order catalog/i })).toBeVisible();
  await expect(page.locator("main")).toContainText(/Harbor|FreshLine|Arctic|Pantry/i);
  const add = page.getByRole("button", { name: /add to cart/i }).first();
  await expect(add).toBeEnabled();
  const cartUpdate = page.waitForResponse(response =>
    response.url().includes("/api/v1/shop/stores/")
      && response.url().endsWith("/cart")
      && response.request().method() === "PUT");
  await add.click();
  expect((await cartUpdate).status()).toBe(200);

  await page.goto(`${dashboardUrl}/shop/cart`);
  const place = page.getByRole("button", { name: "Place order", exact: true });
  await expect(place).toBeEnabled();
  await place.click();
  await expect(page).toHaveURL(/\/shop\/orders\/[0-9a-f-]+$/i);
  await expect(async () => {
    await page.reload();
    await expect(page.getByText("Warehouse confirmed", { exact: true })).toBeVisible();
  }).toPass({ timeout: 90_000, intervals: [2_000, 5_000] });
});
