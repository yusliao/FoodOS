import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";
import en from "../../src/i18n/locales/en-US.json" with { type: "json" };
const id = "33333333-3333-3333-3333-333333333333";
const ticket = { id, number: "TK-1", title: "Concurrent ticket", status: "Open", priority: "Low", customerTenantId: "acme", reporterUserId: "reporter", assignedToUserId: "existing", createdAtUtc: "2026-09-17T00:00:00Z", commentCount: 0 };

test("assignee lookup retries without losing selection and cancel aborts pending search", async ({ page }) => {
  const permissions = ["Permissions.Tickets.View", "Permissions.Tickets.Assign", "Permissions.Users.View"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: ticket }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
  let failed = true;
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/identity/users/search?**", async route => {
    expect(route.request().headers().tenant).toBe("root");
    const search = new URL(route.request().url()).searchParams.get("Search");
    if (search === "pending") await held;
    if (failed) return route.fulfill({ status: 403, json: { detail: "Lookup denied" } });
    return route.fulfill({ json: paged([{ id: "other", userName: "Other employee", isActive: true }]) });
  });
  await page.goto(`/tickets/${id}`);
  await page.getByRole("button", { name: en.tickets.actions.assign, exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("alert")).toContainText("Lookup denied");
  await expect(dialog.getByLabel(en.tickets.assignee, { exact: true })).toHaveValue("existing");
  failed = false;
  await dialog.getByRole("button", { name: en.workbench.retry }).click();
  await expect(dialog.getByRole("option", { name: "Other employee" })).toHaveCount(1);
  await expect(dialog.getByLabel(en.tickets.assignee, { exact: true })).toHaveValue("existing");
  const started = page.waitForRequest(request => request.url().includes("Search=pending"));
  await dialog.getByLabel(en.tickets.searchAssignees).fill("pending");
  const request = await started;
  const aborted = page.waitForEvent("requestfailed", { predicate: value => value === request });
  await dialog.getByRole("button", { name: en.chrome.cancel, exact: true }).click();
  await aborted;
  release();
  await expect(dialog).toHaveCount(0);
});

test("state conflict refreshes authoritative ticket and removes a now invalid action", async ({ page }) => {
  const permissions = ["Permissions.Tickets.View", "Permissions.Tickets.Resolve", "Permissions.Tickets.Reopen"];
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  let state = "Open", writes = 0;
  await page.route(`**/api/v1/tickets/${id}`, route => route.fulfill({ json: { ...ticket, status: state } }));
  await page.route(`**/api/v1/tickets/${id}/comments`, route => route.fulfill({ json: [] }));
  await page.route(`**/api/v1/tickets/${id}/resolve`, route => {
    writes++;
    state = "Closed";
    return route.fulfill({ status: 409, json: { detail: "Already closed by another operator" } });
  });
  await page.goto(`/tickets/${id}`);
  await page.getByRole("button", { name: en.tickets.actions.resolve, exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: en.common.confirm, exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByText(en.tickets.status.Closed, { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: en.tickets.actions.resolve, exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: en.tickets.actions.reopen, exact: true })).toBeVisible();
  expect(writes).toBe(1);
});
