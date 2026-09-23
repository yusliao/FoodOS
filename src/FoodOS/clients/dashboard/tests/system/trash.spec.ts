import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test("operator recycle bin is retired without loading deleted records", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({
      json: [
        "Permissions.Catalog.Products.Restore",
        "Permissions.Catalog.Brands.Restore",
        "Permissions.Catalog.Categories.Restore",
      ],
    }),
  );
  const requests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/v1/") && request.url().includes("/trash")) {
      requests.push(request.url());
    }
  });

  await page.goto("/system/trash");

  await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
  await expect(page.getByText("Retired route: /system/trash")).toBeVisible();
  expect(requests).toEqual([]);
});
