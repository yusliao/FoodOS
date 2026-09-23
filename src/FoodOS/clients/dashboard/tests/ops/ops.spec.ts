import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await page.route("**/api/v1/identity/permissions", (route) =>
    route.fulfill({
      json: [
        "Permissions.Procurement.Purchase.View",
        "Permissions.Warehouse.Waves.View",
        "Permissions.Logistics.Shipments.View",
      ],
    }),
  );
});

for (const path of ["/ops/purchase", "/ops/qc", "/ops/putaway", "/ops/waves", "/ops/picks", "/ops/shipments"]) {
  test(`internal operations route is retired: ${path}`, async ({ page }) => {
    const requests: string[] = [];
    page.on("request", (request) => {
      if (/\/api\/v1\/(procurement|warehouse|inventory|logistics)/.test(request.url())) {
        requests.push(request.url());
      }
    });

    await page.goto(path);

    await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
    await expect(page.getByText(`Retired route: ${path}`)).toBeVisible();
    expect(requests).toEqual([]);
  });
}

test("internal operations are absent from navigation and commands", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("button", { name: "Fulfillment", exact: true })).toHaveCount(0);
  await page.keyboard.press("Control+k");
  await expect(page.getByRole("option", { name: /Purchasing/i })).toHaveCount(0);
});
