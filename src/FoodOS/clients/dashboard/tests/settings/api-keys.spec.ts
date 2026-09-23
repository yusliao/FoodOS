import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test("legacy API-key settings route is retired from the restaurant portal", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);

  await page.goto("/settings/api-keys");

  await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
  await expect(page.getByText("Retired route: /settings/api-keys")).toBeVisible();
  await expect(page.locator('a[href="/settings/api-keys"]')).toHaveCount(0);
});
