import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

const view = "Permissions.Webhooks.View";
const permission = (action: string) => `Permissions.Webhooks.${action}`;
const sub = { id: "wh-1", url: "https://example.test/hooks", events: ["tenant.created"], isActive: true, createdAtUtc: "2026-09-01T00:00:00Z" };

async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const requests: { path: string; method: string; body: unknown }[] = [];
  await page.route("**/api/v1/webhooks/**", async route => {
    const request = route.request();
    expect(request.headers().tenant).toBe("root");
    const url = new URL(request.url());
    requests.push({ path: url.pathname, method: request.method(), body: request.postData() ? request.postDataJSON() : null });
    if (url.pathname.endsWith("/test")) return route.fulfill({ json: { success: true } });
    if (request.method() === "DELETE") return route.fulfill({ status: 204 });
    if (request.method() === "POST") return route.fulfill({ json: "wh-2" });
    if (url.pathname.endsWith("/deliveries")) return route.fulfill({ json: paged([]) });
    expect(Number(url.searchParams.get("pageSize"))).toBeLessThanOrEqual(100);
    return route.fulfill({ json: paged([sub]) });
  });
  return requests;
}

test("viewer can navigate list and detail without write actions", async ({ page }) => {
  const requests = await setup(page, [view]);
  await page.goto("/");
  await page.getByRole("link", { name: "Webhooks", exact: true }).first().click();
  await page.getByRole("button", { name: new RegExp(sub.url) }).click();
  await expect(page.getByRole("heading", { name: "Deliveries", exact: true })).toBeVisible();
  await expect(page.getByText("No deliveries yet. Waiting for matching events.", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: /New subscription|Send test event|Delete subscription/ })).toHaveCount(0);
  expect(requests.every(request => request.method === "GET")).toBe(true);
});

for (const path of ["/webhooks", "/webhooks/wh-1"]) {
  test(`write permissions cannot bypass View at ${path}`, async ({ page }) => {
    const requests = await setup(page, [permission("Create"), permission("Test"), permission("Delete")]);
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
    await expect(page.getByRole("link", { name: "Webhooks", exact: true })).toHaveCount(0);
    expect(requests).toEqual([]);
  });
}

test("viewer empty list offers no create action", async ({ page }) => {
  await setup(page, [view]);
  await page.route("**/api/v1/webhooks/subscriptions?*", route => route.fulfill({ json: paged([]) }));
  await page.goto("/webhooks");
  await expect(page.getByText("No webhook subscriptions yet.", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "New subscription" })).toHaveCount(0);
});

test("Create only submits captured form and preserves input after failure", async ({ page }) => {
  const requests = await setup(page, [view, permission("Create")]);
  let fail = true;
  await page.route("**/api/v1/webhooks/subscriptions", async route => {
    if (fail) return route.fulfill({ status: 403, json: { detail: "Create denied" } });
    return route.fallback();
  });
  await page.goto("/webhooks");
  await page.getByRole("button", { name: "New subscription" }).click();
  await page.getByLabel("Endpoint URL").fill("https://example.test/new-hook");
  await page.getByRole("button", { name: "tenant.created", exact: true }).click();
  await page.getByRole("button", { name: "Create subscription", exact: true }).click();
  await expect(page.getByText("Create denied", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Endpoint URL")).toHaveValue("https://example.test/new-hook");
  fail = false;
  await page.getByRole("button", { name: "Create subscription", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(requests.find(request => request.method === "POST")?.body).toEqual({ url: "https://example.test/new-hook", events: ["tenant.created"], secret: null });
  await expect(page.getByRole("button", { name: /^(Test|Delete subscription)/ })).toHaveCount(0);
});

for (const detail of [false, true]) {
  test(`Test permission only sends test from ${detail ? "detail" : "list"}`, async ({ page }) => {
    const requests = await setup(page, [view, permission("Test")]);
    await page.goto(detail ? "/webhooks/wh-1" : "/webhooks");
    await page.getByRole("button", { name: detail ? "Send test event" : "Test", exact: true }).click();
    await expect(page.getByText("Test event delivered", { exact: true })).toBeVisible();
    expect(requests.filter(request => request.method === "POST").map(request => request.path)).toEqual(["/api/v1/webhooks/subscriptions/wh-1/test"]);
    await expect(page.getByRole("button", { name: /New subscription|Delete subscription/ })).toHaveCount(0);
  });

  test(`Delete permission only deletes from ${detail ? "detail" : "list"}`, async ({ page }) => {
    const requests = await setup(page, [view, permission("Delete")]);
    await page.goto(detail ? "/webhooks/wh-1" : "/webhooks");
    page.once("dialog", dialog => dialog.accept());
    await page.getByRole("button", { name: /Delete subscription/ }).click();
    await expect(page.getByText("Subscription deleted", { exact: true })).toBeVisible();
    await expect(page).toHaveURL(/\/webhooks$/);
    expect(requests.filter(request => request.method === "DELETE")).toHaveLength(1);
    await expect(page.getByRole("button", { name: /New subscription|^Test$/ })).toHaveCount(0);
  });
}

test("detail scans legal pages, retries failures and only then reports missing", async ({ page }) => {
  await setup(page, [view]);
  const pages: number[] = [];
  let fail = true;
  await page.route("**/api/v1/webhooks/subscriptions?*", route => {
    const url = new URL(route.request().url());
    const pageNumber = Number(url.searchParams.get("pageNumber"));
    expect(url.searchParams.get("pageSize")).toBe("100");
    pages.push(pageNumber);
    if (fail) return route.fulfill({ status: 403, json: { detail: "Directory denied" } });
    return route.fulfill({ json: paged(pageNumber === 2 ? [sub] : [{ ...sub, id: "other" }], { pageNumber, pageSize: 100, totalPages: 2, totalCount: 101 }) });
  });
  await page.goto("/webhooks/wh-1");
  await expect(page.getByText("Directory denied", { exact: true })).toBeVisible();
  await expect(page.getByText(/Subscription not found/)).toHaveCount(0);
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("heading", { name: sub.url, exact: true })).toBeVisible();
  expect(pages).toContain(2);
  await page.goto("/webhooks/missing");
  await expect(page.getByText("Subscription not found. It may have been deleted.", { exact: true })).toBeVisible();
});

test("directory failure can refresh and delivery failure can retry", async ({ page }) => {
  await setup(page, [view]);
  let fail = true;
  await page.route("**/api/v1/webhooks/**", route => fail
    ? route.fulfill({ status: 500, json: { detail: "Temporarily unavailable" } }) : route.fallback());
  await page.goto("/webhooks");
  await expect(page.getByText("Temporarily unavailable", { exact: true })).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await page.getByRole("button", { name: new RegExp(sub.url) }).click();
  await expect(page.getByRole("heading", { name: "Deliveries", exact: true })).toBeVisible();
  fail = true;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText("Temporarily unavailable", { exact: true })).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText("Temporarily unavailable", { exact: true })).toHaveCount(0);
});

test("Chinese mobile detail and list fit long URLs and event names", async ({ page }) => {
  await setup(page, [view, permission("Test"), permission("Delete")]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  const longSub = { ...sub, url: `https://example.test/${"long".repeat(30)}`, events: ["event.".repeat(30)] };
  await page.route("**/api/v1/webhooks/subscriptions?*", route => route.fulfill({ json: paged([longSub]) }));
  for (const path of ["/webhooks", "/webhooks/wh-1"]) {
    await page.goto(path);
    await expect(page.getByText(longSub.url, { exact: true }).first()).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
});

test("test rejection and delete failure retain the detail for retry", async ({ page }) => {
  await setup(page, [view, permission("Test"), permission("Delete")]);
  let fail = true;
  await page.route("**/api/v1/webhooks/subscriptions/wh-1/test", route => route.fulfill({ json: { success: false } }));
  await page.route("**/api/v1/webhooks/subscriptions/wh-1", route => fail
    ? route.fulfill({ status: 403, json: { detail: "Delete denied" } }) : route.fallback());
  await page.goto("/webhooks/wh-1");
  await page.getByRole("button", { name: "Send test event", exact: true }).click();
  await expect(page.getByText("Endpoint rejected the test event", { exact: true })).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Delete subscription", exact: true }).click();
  await expect(page.getByText("Delete denied", { exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\/webhooks\/wh-1$/);
  fail = false;
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Delete subscription", exact: true }).click();
  await expect(page).toHaveURL(/\/webhooks$/);
});

test("list test HTTP error is visible and can be retried", async ({ page }) => {
  await setup(page, [view, permission("Test")]);
  let fail = true;
  await page.route("**/api/v1/webhooks/subscriptions/wh-1/test", route => fail
    ? route.fulfill({ status: 500, json: { detail: "Test unavailable" } }) : route.fallback());
  await page.goto("/webhooks");
  await page.getByRole("button", { name: "Test", exact: true }).click();
  await expect(page.getByText("Test unavailable", { exact: true })).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Test", exact: true }).click();
  await expect(page.getByText("Test event delivered", { exact: true })).toBeVisible();
});

test("loading directory does not masquerade as empty or trigger deliveries", async ({ page }) => {
  const requests = await setup(page, [view]);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/webhooks/subscriptions?*", async route => {
    await pending;
    await route.fallback();
  });
  await page.goto("/webhooks/wh-1");
  await expect(page.getByText("Loading subscription…", { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/Subscription not found/)).toHaveCount(0);
  expect(requests).toEqual([]);
  release();
  await expect(page.getByRole("heading", { name: sub.url, exact: true })).toBeVisible();
  await expect(page.getByText("No deliveries yet. Waiting for matching events.", { exact: true })).toBeVisible();
});
