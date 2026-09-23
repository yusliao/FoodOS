import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { mockJsonResponse } from "../helpers/api-mocks";

const NOTIFICATION_PERMISSIONS = [
  "Permissions.Notifications.Inbox.View",
  "Permissions.Notifications.Inbox.MarkRead",
];

// Per-user notification preference persistence isn't built yet — the page
// is an honest placeholder that points users at the in-app bell. There's
// no preferences GET/PUT to mock beyond the shell defaults. Specs assert
// the placeholder copy + the bell affordance.
test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
  await mockJsonResponse(page, "**/api/v1/identity/permissions", NOTIFICATION_PERMISSIONS);
});

test.describe("settings/notifications — placeholder", () => {
  test("renders the preferences section and the placeholder copy", async ({ page }) => {
    await page.goto("/settings/notifications");

    // Section title is an <h3>; the sidebar nav uses plain spans, so the
    // heading role is unambiguous.
    await expect(
      page.getByRole("heading", { name: "Notification preferences" }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: /per-event preferences aren't tunable yet/i }),
    ).toBeVisible();
    await expect(page.getByText(/granular per-event email opt-ins/i)).toBeVisible();
  });

  test("exposes a button that opens the notifications bell", async ({ page }) => {
    await page.goto("/settings/notifications");

    await expect(
      page.getByRole("button", { name: /open notifications bell/i }),
    ).toBeVisible();
  });

  test("keeps the customer notification bell available from settings", async ({ page }) => {
    await page.goto("/settings/notifications");

    await expect(page.locator("[data-notification-bell]")).toBeVisible();
    await page.getByRole("button", { name: /open notifications bell/i }).click();
    await expect(page).toHaveURL(/\/settings\/notifications$/);
  });

  test("does not mount the notification inbox without view permission", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/identity/permissions", []);
    let unreadRequests = 0;
    page.on("request", (request) => {
      if (request.url().includes("/api/v1/notifications/unread-count")) unreadRequests += 1;
    });

    await page.goto("/settings/notifications");

    await expect(page.locator("[data-notification-bell]")).toHaveCount(0);
    expect(unreadRequests).toBe(0);
  });
});
