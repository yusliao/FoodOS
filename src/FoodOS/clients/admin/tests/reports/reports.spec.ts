import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const permission = "Permissions.Ops.Kpis.View";
const result = { date: "2026-09-17", fulfillmentRate: 0.75, stockoutRate: 0.125, shrinkageRate: 0.025, temperatureComplianceRate: null, committedOrderCount: 4, fulfilledOrderCount: 3, orderedQty: 80, inboundQty: 200, lossQty: 5 };
async function setup(page: Page, permissions = [permission]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: string[] = [];
  await page.route("**/api/v1/ops/kpis**", async route => {
    expect(route.request().method()).toBe("GET");
    expect(route.request().headers().tenant).toBe("root");
    requests.push(route.request().url());
    await route.fulfill({ json: result });
  });
  return requests;
}

test("report viewer navigates from workbench and sees all metrics and source boundaries", async ({ page }) => {
  await setup(page);
  await page.goto("/");
  await page.getByRole("main").getByRole("link", { name: "Operations reports", exact: true }).click();
  await expect(page).toHaveURL(/\/reports$/);
  await expect(page.getByRole("heading", { name: "Operations reports", exact: true })).toBeVisible();
  await expect(page.getByText("75%", { exact: true })).toBeVisible();
  await expect(page.getByText("12.5%", { exact: true })).toBeVisible();
  await expect(page.getByText("Not available", { exact: true })).toBeVisible();
  await expect(page.locator("dl dt")).toHaveCount(9);
  await expect(page.getByRole("note")).toContainText("not live WMS stock");
});

test("no permission hides report link and direct URL makes no KPI request", async ({ page }) => {
  const requests = await setup(page, []);
  await page.goto("/");
  await expect(page.getByRole("link", { name: "Operations reports", exact: true })).toHaveCount(0);
  await page.goto("/reports");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toEqual([]);
});

test("date switching uses request date and invalid or cleared dates make no request", async ({ page }) => {
  const requests = await setup(page);
  await page.goto("/reports");
  const date = page.getByLabel("Report date", { exact: true });
  await date.fill("2025-01-02");
  await expect.poll(() => requests.some(url => url.endsWith("date=2025-01-02"))).toBe(true);
  const count = requests.length;
  await date.fill("1999-12-31");
  await expect(page.getByText("Choose a valid date from 2000-01-01 through 2100-12-31.")).toBeVisible();
  await date.fill("");
  await expect(page.locator("dl")).toHaveCount(0);
  expect(requests).toHaveLength(count);
});

for (const status of [403, 500]) test(`${status} failure is retryable without showing metrics`, async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/ops/kpis**", async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status, json: { detail: "Report request rejected" } });
  });
  await page.goto("/reports");
  await expect(page.getByText("Report request rejected")).toBeVisible();
  await expect(page.locator("dl")).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("75%", { exact: true })).toBeVisible();
});

test("zero activity and null temperature remain distinct in Chinese mobile report", async ({ page }) => {
  await setup(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.route("**/api/v1/ops/kpis**", route => route.fulfill({ json: { ...result, fulfillmentRate: 0, stockoutRate: 0, shrinkageRate: 0, committedOrderCount: 0, fulfilledOrderCount: 0, orderedQty: 0, inboundQty: 0, lossQty: 0 } }));
  await page.goto("/reports");
  await expect(page.getByRole("heading", { name: "经营报表", exact: true })).toBeVisible();
  await expect(page.getByText("该日期没有记录订单或库存活动；温控数据尚不可用。", { exact: true })).toBeVisible();
  await expect(page.getByText("暂无数据", { exact: true })).toBeVisible();
  await expect(page.getByText("0%", { exact: true })).toHaveCount(3);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("loading a different date hides old metrics until the response arrives", async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/ops/kpis?date=2020-01-01", async route => { await gate; await route.fulfill({ json: { ...result, date: "2020-01-01", fulfillmentRate: 0.5 } }); });
  await page.goto("/reports");
  await expect(page.getByText("75%", { exact: true })).toBeVisible();
  await page.getByLabel("Report date", { exact: true }).fill("2020-01-01");
  try {
    await expect(page.getByText("Loading report…", { exact: true })).toBeVisible();
    await expect(page.getByText("75%", { exact: true })).toHaveCount(0);
  } finally { release(); }
  await expect(page.getByText("50%", { exact: true })).toBeVisible();
  await expect(page.locator("time")).toHaveText("2020-01-01");
});
