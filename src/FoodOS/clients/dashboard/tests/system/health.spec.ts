import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";

test("operator health route is retired without probing dependencies", async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  const requests: string[] = [];
  page.on("request", (request) => {
    if (/\/health\/(ready|live)/.test(request.url())) requests.push(request.url());
  });

  await page.goto("/system/health");

  await expect(page.getByRole("heading", { name: "This page has moved" })).toBeVisible();
  await expect(page.getByText("Retired route: /system/health")).toBeVisible();
  expect(requests).toEqual([]);
});
