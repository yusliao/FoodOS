import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test("full audit route is retired without loading audit data", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({ json: ["Permissions.AuditTrails.View"] }),
  );
  const requests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/v1/audit")) requests.push(request.url());
  });

  await page.goto("/system/audits");

  await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
  await expect(page.getByText("Retired route: /system/audits")).toBeVisible();
  expect(requests).toEqual([]);
});
