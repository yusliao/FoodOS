import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test("all-user sessions route is retired while personal security remains", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({ json: ["Permissions.Sessions.ViewAll"] }),
  );
  const adminSessionRequests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/v1/identity/sessions/tenant")) {
      adminSessionRequests.push(request.url());
    }
  });

  await page.goto("/system/sessions");

  await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
  await expect(page.getByText("Retired route: /system/sessions")).toBeVisible();
  await expect(page.getByRole("link", { name: "Settings" })).toBeVisible();
  expect(adminSessionRequests).toEqual([]);
});
