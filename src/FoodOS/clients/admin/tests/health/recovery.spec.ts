import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
import zh from "../../src/i18n/locales/zh-CN.json" with { type: "json" };

const permissions = ["Permissions.Tenants.View"];
const healthy = { status: "Healthy", results: [] };

test("pending probes disable refresh and settle to a genuine empty report", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const releases: (() => void)[] = [];
  await page.route(/\/health\/(live|ready)$/, async route => {
    await new Promise<void>(resolve => releases.push(resolve));
    await route.fulfill({ json: healthy });
  });
  await page.goto("/health");
  const main = page.getByRole("main");
  // StrictMode can cancel and restart the initial signal-aware queries.
  await expect.poll(() => releases.length).toBeGreaterThanOrEqual(2);
  await expect(main.getByRole("button", { name: en.health.refresh, exact: true })).toBeDisabled();
  await expect(main.getByText(en.health.probing, { exact: true })).toHaveCount(2);
  releases.forEach(release => release());
  await expect(main.getByRole("button", { name: en.health.refresh, exact: true })).toBeEnabled();
  await expect(main.getByText(en.health.noChecks, { exact: true })).toHaveCount(2);
});

test("no permission hides navigation and direct URL sends no probes", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
  await installAdminShellMocks(page, []);
  const probes: string[] = [];
  page.on("request", request => { if (/\/health\/(live|ready)/.test(request.url())) probes.push(request.url()); });
  await page.goto("/health");
  await expect(page.getByRole("main")).toContainText(/access denied|permission/i);
  await expect(page.getByRole("link", { name: "Health", exact: true })).toHaveCount(0);
  expect(probes).toEqual([]);
});

for (const kind of ["403", "html", "malformed"]) {
  test(`invalid probe ${kind} stays unknown and refresh recovers`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    let fail = true;
    await page.route("**/health/live", route => route.fulfill({ json: healthy }));
    await page.route("**/health/ready", route => {
      expect(route.request().headers().authorization).toBeUndefined();
      expect(route.request().headers().tenant).toBeUndefined();
      if (!fail) return route.fulfill({ json: healthy });
      if (kind === "403") return route.fulfill({ status: 403 });
      if (kind === "html") return route.fulfill({ contentType: "text/html", body: "<html>Login page</html>" });
      return route.fulfill({ json: { status: "Healthy", results: [{ name: "db", status: "Healthy" }] } });
    });
    await page.goto("/health");
    const main = page.getByRole("main");
    await expect(main.getByText(en.health.readinessFailed, { exact: true })).toBeVisible();
    await expect(main.getByText(en.health.statusUnknown, { exact: true })).toBeVisible();
    fail = false;
    await main.getByRole("button", { name: en.health.refresh, exact: true }).click();
    await expect(main.getByText(en.health.readinessFailed, { exact: true })).toHaveCount(0);
    await expect(main.getByText(en.health.statusUnknown, { exact: true })).toHaveCount(0);
  });
}

for (const culture of ["en-US", "zh-CN"]) {
  test(`failed refresh removes stale healthy report · ${culture}`, async ({ page }) => {
    const t = culture === "en-US" ? en.health : zh.health;
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    await page.addInitScript(c => localStorage.setItem("foodos.culture", c), culture);
    await page.setViewportSize({ width: 390, height: 844 });
    let fail = false;
    await page.route("**/health/live", route => route.fulfill({ json: healthy }));
    await page.route("**/health/ready", route => fail ? route.fulfill({ status: 500 }) : route.fulfill({
      json: { status: "Healthy", results: [{ name: "database", status: "Healthy", durationMs: 1, details: { version: "test" } }] },
    }));
    await page.goto("/health");
    const main = page.getByRole("main");
    await expect(main.getByText("database", { exact: true })).toBeVisible();
    fail = true;
    await main.getByRole("button", { name: t.refresh, exact: true }).click();
    await expect(main.getByText(t.readinessFailed, { exact: true })).toBeVisible();
    await expect(main.getByText("database", { exact: true })).toHaveCount(0);
    await expect(main.getByText(t.statusUnknown, { exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}
