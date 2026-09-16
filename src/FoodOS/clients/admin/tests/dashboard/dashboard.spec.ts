import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, ADMIN_PERMS } from "../helpers/shell-mocks";
import { mockJsonResponse } from "../helpers/api-mocks";

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [...ADMIN_PERMS] });
  await installAdminShellMocks(page);
  await mockJsonResponse(page, "**/api/v1/ops/kpis**", {
    date: "2026-09-12", fulfillmentRate: 0.75, stockoutRate: 0.1, shrinkageRate: 0.05,
    temperatureComplianceRate: null, committedOrderCount: 4, fulfilledOrderCount: 3,
    orderedQty: 20, inboundQty: 40, lossQty: 2,
  });
});

test("operator home has authorized entry points, without subscription statistics requests", async ({ page }) => {
  const irrelevant: string[] = [];
  page.on("request", request => {
    if (/\/api\/v1\/(tenants|billing)/.test(new URL(request.url()).pathname)) irrelevant.push(request.url());
  });
  await page.goto("/");
  const main = page.getByRole("main");
  await expect(main.getByRole("heading", { name: "Operator workbench" })).toBeVisible();
  await expect(main.getByRole("heading", { name: "System administration" })).toBeVisible();
  await expect(main.getByRole("link", { name: "Employees", exact: true })).toBeVisible();
  await expect(main.getByRole("link", { name: "Software subscriptions" })).toBeVisible();
  expect(irrelevant).toEqual([]);
  await page.screenshot({ path: test.info().outputPath("operator-workbench.png"), fullPage: true });
});

test("authorized operations KPIs show real rates and unavailable temperature", async ({ page }) => {
  await page.goto("/");
  const main = page.getByRole("main");
  await expect(main.getByText("75.0%", { exact: true })).toBeVisible();
  await expect(main.getByText("N/A", { exact: true })).toBeVisible();
});

test("revoked cached permissions do not render links or make unauthorized requests", async ({ page }) => {
  await installAdminShellMocks(page, []);
  const denied: string[] = [];
  page.on("request", request => {
    if (/\/api\/v1\/(ops|tenants|billing|notifications)/.test(new URL(request.url()).pathname)) denied.push(request.url());
  });
  await page.goto("/");
  await expect(page.getByText("No work modules are available for your current permissions.", { exact: false })).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: "Employees", exact: true })).toHaveCount(0);
  await expect(page.getByRole("main").getByRole("link", { name: "Settings", exact: true })).toBeVisible();
  expect(denied).toEqual([]);
  await page.goto("/users");
  await expect(page.getByRole("main")).toContainText(/Access denied|Forbidden/i);
});

test("report failure is explicit and can be retried", async ({ page }) => {
  await page.route("**/api/v1/ops/kpis**", route => route.fulfill({ status: 403, contentType: "application/json", body: "{}" }));
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText("Operations data could not be loaded");
  await expect(page.getByRole("button", { name: "Retry", exact: true })).toBeVisible();
});

test("Chinese workbench labels follow the existing locale setting", async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/");
  await expect(page.getByRole("main").getByRole("heading", { name: "运营工作台" })).toBeVisible();
  await expect(page.getByRole("main").getByRole("heading", { name: "系统管理" })).toBeVisible();
});

test("mobile navigation uses the same employee permissions and hides system tools", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await installAdminShellMocks(page, ["Permissions.Users.View"]);
  await page.goto("/");
  await expect(page.getByRole("main").getByRole("link", { name: "Employees", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Open navigation menu" }).click();
  const drawer = page.getByRole("dialog");
  await expect(drawer.getByRole("button", { name: "System administration" })).toHaveCount(0);
  await drawer.getByRole("button", { name: "Operator team" }).click();
  await expect(drawer.getByRole("link", { name: "Employees", exact: true })).toBeVisible();
  await expect(drawer.getByRole("link", { name: "Roles", exact: true })).toHaveCount(0);
  await page.screenshot({ path: test.info().outputPath("operator-mobile-nav.png"), fullPage: true });
});
