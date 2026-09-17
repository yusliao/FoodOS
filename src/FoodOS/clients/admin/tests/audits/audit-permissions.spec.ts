import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.AuditTrails.View";
const cross = "Permissions.AuditTrails.ViewCrossTenant";
const row = { id: "audit-1", occurredAtUtc: "2026-09-17T00:00:00Z", eventType: "Security", severity: "Information", tenantId: "root", source: "Audit test source", userName: "operator", tags: 0 };
const summary = { eventsByType: { Security: 7 }, eventsBySeverity: { Information: 7 }, eventsBySource: {}, eventsByTenant: { root: 7 } };
async function setup(page: Page, permissions = [view]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: URL[] = [];
  await page.route("**/api/v1/audits/**", async route => {
    expect(route.request().headers().tenant).toBe("root");
    expect(route.request().method()).toBe("GET");
    const url = new URL(route.request().url()); requests.push(url);
    await route.fulfill({ json: url.pathname.endsWith("/summary") ? summary : url.pathname.endsWith("/audit-1") ? { ...row, receivedAtUtc: row.occurredAtUtc, payload: { action: "checked" } } : paged([{ ...row, tenantId: url.searchParams.get("TenantId") ?? "root" }]) });
  });
  return requests;
}

test("no view permission prevents all audit requests", async ({ page }) => {
  const requests = await setup(page, [cross]);
  await page.goto("/audits");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toEqual([]);
});

test("viewer cannot inject cross-domain scope through URL", async ({ page }) => {
  const requests = await setup(page);
  await page.goto("/audits?tenant=restaurant-a");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toEqual([]);
});

test("cross-domain reader uses query parameter without identity switch or unsupported detail", async ({ page }) => {
  const requests = await setup(page, [view, cross]);
  await page.goto("/audits?tenant=restaurant-a");
  await expect(page.getByText("Audit test source", { exact: true })).toBeVisible();
  await expect(page.getByRole("button").filter({ hasText: "Audit test source" })).toBeDisabled();
  await expect(page.getByText(/Cross-domain access supports list summaries only/)).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests.every(url => url.searchParams.get("TenantId") === "restaurant-a")).toBe(true);
});

test("summary failure is unavailable instead of zero and retries independently", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/audits/summary*", async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Summary denied" } });
  });
  await page.goto("/audits");
  await expect(page.getByText("Audit summary unavailable. Counts are not zero; retry to load them.")).toBeVisible();
  await expect(page.getByText("Total events", { exact: true })).toHaveCount(0);
  await expect(page.getByText("Audit test source", { exact: true })).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("Total events", { exact: true })).toBeVisible();
});

test("root detail 403 can be retried without changing tenant", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/api/v1/audits/audit-1", async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Detail denied" } });
  });
  await page.goto("/audits");
  await page.getByText("Audit test source", { exact: true }).click();
  const sheet = page.getByRole("dialog");
  await expect(sheet.getByText("Detail denied")).toBeVisible();
  fail = false;
  await sheet.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(sheet.getByText(/"action": "checked"/)).toBeVisible();
});

test("Chinese mobile list fits and unauthorized tenant filter is absent", async ({ page }) => {
  await setup(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/audits");
  await expect(page.getByText("Audit test source", { exact: true })).toBeVisible();
  await expect(page.locator("main input")).toHaveCount(2);
  await expect(page.getByText("汇总为所选身份域最近 7 天的数据，不随列表搜索、事件类型、级别或关联编号筛选变化。")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("list failure refreshes successfully and refresh also reloads summary", async ({ page }) => {
  const requests = await setup(page);
  let fail = true;
  await page.route("**/api/v1/audits/?*", async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "List denied" } });
  });
  await page.goto("/audits");
  await expect(page.getByText("List denied")).toBeVisible();
  const summaries = requests.filter(url => url.pathname.endsWith("/summary")).length;
  fail = false;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText("Audit test source", { exact: true })).toBeVisible();
  await expect.poll(() => requests.filter(url => url.pathname.endsWith("/summary")).length).toBeGreaterThan(summaries);
});

for (const path of ["/tenants", "/billing", "/webhooks", "/impersonation", "/health"]) test(`audit permission does not grant system route ${path}`, async ({ page }) => {
  await setup(page);
  const calls: string[] = [];
  await page.route("**/api/v1/**", async route => {
    if (/\/identity\/(profile|permissions)$/.test(new URL(route.request().url()).pathname)) return route.fallback();
    calls.push(route.request().url());
    await route.fulfill({ json: {} });
  });
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(calls).toEqual([]);
});
