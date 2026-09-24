import { expect, test } from "@playwright/test";
import { createHmac } from "node:crypto";

const apiBase = process.env.PLAYWRIGHT_REAL_API_URL ?? "http://localhost:5030";
const tenant = "acme";
const email = "admin@acme.com";
const password = "Password123!";

function createTotp(sharedKey: string) {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  const normalized = sharedKey.replace(/=+$/u, "").replace(/\s/gu, "").toUpperCase();
  let bits = "";
  for (const character of normalized) {
    const value = alphabet.indexOf(character);
    if (value < 0) throw new Error("The authenticator key is not valid Base32.");
    bits += value.toString(2).padStart(5, "0");
  }

  const key = Buffer.from(
    Array.from({ length: Math.floor(bits.length / 8) }, (_, index) =>
      Number.parseInt(bits.slice(index * 8, index * 8 + 8), 2)),
  );
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000)));
  const digest = createHmac("sha1", key).update(counter).digest();
  const offset = digest[digest.length - 1] & 0x0f;
  const binary = (
    ((digest[offset] & 0x7f) << 24)
    | ((digest[offset + 1] & 0xff) << 16)
    | ((digest[offset + 2] & 0xff) << 8)
    | (digest[offset + 3] & 0xff)
  ) >>> 0;
  return (binary % 1_000_000).toString().padStart(6, "0");
}

test("restaurant signs in and reads its quoted catalog from the real API", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Tenant").fill(tenant);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);

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

test("restaurant completes a real authenticator challenge and can disable it again", async ({ page, request }) => {
  let sharedKey = "";
  let enabled = false;

  try {
    await page.goto("/login");
    await page.getByLabel("Tenant").fill(tenant);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(password);
    await page.getByRole("button", { name: /^sign in$/i }).click();
    await expect(page).not.toHaveURL(/\/login$/);

    await page.goto("/settings/security");
    const enrollResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/enroll")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: "Enable two-factor", exact: true }).click();
    expect((await enrollResponse).status()).toBe(200);
    sharedKey = (await page.locator("code").first().textContent())?.trim() ?? "";
    expect(sharedKey).not.toBe("");

    const verifyResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/verify")
      && response.request().method() === "POST");
    await page.getByLabel("6-digit code from your app").fill(createTotp(sharedKey));
    await page.getByRole("button", { name: "Confirm & enable", exact: true }).click();
    expect((await verifyResponse).status()).toBe(200);
    enabled = true;
    await expect(page.getByRole("button", { name: "Disable two-factor", exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Open profile menu", exact: true }).click();
    await page.getByRole("menuitem", { name: "Sign out", exact: true }).click();
    await page.getByRole("dialog", { name: "Sign out?" })
      .getByRole("button", { name: "Sign out", exact: true })
      .click();
    await expect(page).toHaveURL(/\/login$/);
    await page.getByLabel("Tenant").fill(tenant);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(password);

    const challengeResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/token/issue")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: /^sign in$/i }).click();
    expect((await challengeResponse).status()).toBe(401);
    await expect(page.getByLabel("Authenticator code")).toBeVisible();

    const loginResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/token/issue")
      && response.request().method() === "POST");
    await page.getByLabel("Authenticator code").fill(createTotp(sharedKey));
    await page.getByRole("button", { name: /^sign in$/i }).click();
    expect((await loginResponse).status()).toBe(200);
    await expect(page).toHaveURL(/\/settings\/security$/);

    await page.getByLabel("Current password").fill(password);
    const disableResponse = page.waitForResponse((response) =>
      response.url().includes("/api/v1/identity/2fa/disable")
      && response.request().method() === "POST");
    await page.getByRole("button", { name: "Disable two-factor", exact: true }).click();
    expect((await disableResponse).status()).toBe(200);
    enabled = false;
    await expect(page.getByRole("button", { name: "Enable two-factor", exact: true })).toBeVisible();
  } finally {
    if (enabled && sharedKey) {
      const tokenResponse = await request.post(`${apiBase}/api/v1/identity/token/issue`, {
        data: { email, password, twoFactorCode: createTotp(sharedKey) },
        headers: { tenant, "X-FSH-App": "dashboard" },
      });
      if (tokenResponse.ok()) {
        const token = await tokenResponse.json() as { accessToken: string };
        await request.post(`${apiBase}/api/v1/identity/2fa/disable`, {
          data: { currentPassword: password },
          headers: { tenant, Authorization: `Bearer ${token.accessToken}` },
        });
      }
    }
  }
});
