import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({ json: ["Permissions.Billing.View"] }),
  );
});

for (const path of ["/subscription", "/invoices", "/invoices/invoice-1"]) {
  test(`software billing route is retired: ${path}`, async ({ page }) => {
    const requests: string[] = [];
    page.on("request", (request) => {
      if (request.url().includes("/api/v1/billing")) requests.push(request.url());
    });

    await page.goto(path);

    await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
    await expect(page.getByText(`Retired route: ${path}`)).toBeVisible();
    expect(requests).toEqual([]);
  });
}
