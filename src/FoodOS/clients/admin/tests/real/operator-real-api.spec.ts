import { expect, test } from "@playwright/test";

test("operator signs in and loads the workbench with real permissions", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByLabel("Tenant")).toHaveValue("root");
  await page.getByLabel("Email").fill("superadmin@root.com");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");

  const tokenResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/identity/token/issue")
    && response.request().method() === "POST");
  const permissionsResponse = page.waitForResponse((response) =>
    response.url().includes("/api/v1/identity/permissions")
    && response.request().method() === "GET");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();

  expect((await tokenResponse).status()).toBe(200);
  const permissions = await permissionsResponse;
  expect(permissions.status()).toBe(200);
  expect((await permissions.json() as string[]).length).toBeGreaterThan(0);
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("main").getByRole("heading", { name: "Operator workbench" })).toBeVisible();
});
