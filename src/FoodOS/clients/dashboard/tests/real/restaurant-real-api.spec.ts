import { expect, test } from "@playwright/test";

test("restaurant signs in and reads its quoted catalog from the real API", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Tenant").fill("acme");
  await page.getByLabel("Email").fill("admin@acme.com");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");

  const tokenResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/identity/token/issue")
    && response.request().method() === "POST");
  await page.getByRole("button", { name: /^sign in$/i }).click();
  expect((await tokenResponse).status()).toBe(200);
  await expect(page).not.toHaveURL(/\/login$/);

  const storesResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/shop/stores")
    && response.request().method() === "GET");
  const productsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/shop/products")
    && response.request().method() === "GET");
  await page.goto("/shop/catalog");

  expect((await storesResponse).status()).toBe(200);
  const products = await productsResponse;
  expect(products.status()).toBe(200);
  const payload = await products.json() as { items: Array<{ name: string; unitPrice: number }> };
  expect(payload.items.length).toBeGreaterThan(0);
  expect(payload.items[0].unitPrice).toBeGreaterThan(0);
  await expect(page.getByRole("heading", { name: /order catalog/i })).toBeVisible();
  await expect(page.getByText(payload.items[0].name).last()).toBeVisible();
});
