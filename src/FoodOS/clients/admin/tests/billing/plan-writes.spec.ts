import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const permissions = ["Permissions.Billing.View", "Permissions.Billing.Manage"];
const plan = { id: "p1", key: "basic", name: "Basic", currency: "USD", monthlyBasePrice: 20, interval: "Monthly", annualPrice: null, overageRates: { ApiCalls: 0.01 }, isActive: true };
async function setup(page: Page) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/billing/plans?*", route => route.fulfill({ json: [plan] }));
  await page.goto("/billing/plans");
}

test("create validates required fields and permits a one-character key", async ({ page }) => {
  await setup(page);
  let body: unknown;
  await page.route("**/api/v1/billing/plans", route => {
    expect(route.request().headers().tenant).toBe("root");
    body = route.request().postDataJSON();
    return route.fulfill({ json: "created" });
  });
  await page.getByRole("button", { name: "New plan", exact: true }).click();
  const submit = page.getByRole("button", { name: "Create plan", exact: true });
  await page.locator("#pf-monthlyBasePrice").fill("20");
  await expect(submit).toBeDisabled();
  await page.locator("#pf-key").fill("p");
  await page.locator("#pf-name").fill("   ");
  await expect(submit).toBeDisabled();
  await page.locator("#pf-name").fill("New plan");
  await page.locator("#pf-currency").fill("US");
  await expect(submit).toBeDisabled();
  await expect(page.getByText("Currency must contain exactly three characters.", { exact: true })).toBeVisible();
  expect(body).toBeUndefined();
  await page.locator("#pf-currency").fill("usd");
  await submit.click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(body).toMatchObject({ key: "p", name: "New plan", currency: "USD", monthlyBasePrice: 20, interval: "Monthly", annualPrice: null });
});

test("create failure retains draft and pending blocks duplicate submit and dismissal", async ({ page }) => {
  await setup(page);
  let calls = 0;
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/billing/plans", async route => {
    calls++;
    if (calls === 1) {
      await pending;
      return route.fulfill({ status: 403, json: { detail: "Create denied" } });
    }
    return route.fulfill({ json: "created" });
  });
  await page.getByRole("button", { name: "New plan", exact: true }).click();
  await page.locator("#pf-key").fill("new");
  await page.locator("#pf-name").fill("Preserve draft");
  await page.locator("#pf-monthlyBasePrice").fill("20");
  await page.getByRole("button", { name: "Create plan", exact: true }).click();
  await expect(page.locator("#pf-name")).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toBeVisible();
  expect(calls).toBe(1);
  release();
  await expect(page.getByText("Create denied", { exact: true })).toBeVisible();
  await expect(page.locator("#pf-name")).toHaveValue("Preserve draft");
  await page.getByRole("button", { name: "Create plan", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(calls).toBe(2);
});

test("edit submits only mutable fields and retains draft on failure", async ({ page }) => {
  await setup(page);
  let calls = 0;
  let body: unknown;
  await page.route("**/api/v1/billing/plans/p1", route => {
    expect(route.request().method()).toBe("PUT");
    expect(route.request().headers().tenant).toBe("root");
    body = route.request().postDataJSON();
    calls++;
    return calls === 1 ? route.fulfill({ status: 500, json: { detail: "Update failed" } }) : route.fulfill({ json: "p1" });
  });
  await page.getByRole("button", { name: "Edit Basic", exact: true }).click();
  await expect(page.locator("#pf-key")).toBeDisabled();
  await expect(page.locator("#pf-currency")).toBeDisabled();
  await page.locator("#pf-name").fill("Updated");
  await page.locator("#pf-monthlyBasePrice").fill("25");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByText("Update failed", { exact: true }).first()).toBeVisible();
  await expect(page.locator("#pf-name")).toHaveValue("Updated");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(body).toEqual({ planId: "p1", name: "Updated", monthlyBasePrice: 25, overageRates: { ApiCalls: 0.01 }, interval: "Monthly", annualPrice: null });
});

test("invalid annual price is ignored after returning to monthly", async ({ page }) => {
  await setup(page);
  await page.getByRole("button", { name: "Edit Basic", exact: true }).click();
  await page.locator("#pf-interval").click();
  await page.getByRole("menuitem", { name: /Yearly/ }).click();
  await page.locator("#pf-annualPrice").fill("-1");
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toBeDisabled();
  await page.locator("#pf-interval").click();
  await page.getByRole("menuitem", { name: /Monthly/ }).click();
  await expect(page.locator("#pf-annualPrice")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Save changes", exact: true })).toBeEnabled();
});
