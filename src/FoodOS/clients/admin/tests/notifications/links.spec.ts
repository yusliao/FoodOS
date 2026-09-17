import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";

const permissions = ["Permissions.Notifications.Inbox.View"];
const entry = { id: "link-test", title: "Linked notification", type: "test", source: "System", readAtUtc: null, createdAtUtc: "2026-09-17T00:00:00Z", metadataJson: "{}" };

for (const surface of ["inbox", "preview"]) {
  for (const destination of ["/orders/order-id", "/ops/shipments?departed=old-id"]) {
    test(`${surface} link respects target permission or missing route: ${destination}`, async ({ page }) => {
      await seedAuthedSession(page, { ...TEST_USER, permissions });
      await installAdminShellMocks(page, permissions);
      await page.route("**/api/v1/notifications/?*", route => route.fulfill({ json: [{ ...entry, link: destination }] }));
      const calls: string[] = [];
      page.on("request", request => {
        if (/\/api\/v1\/(ordering|logistics|warehouse)\//.test(request.url())) calls.push(request.url());
      });
      await page.goto("/notifications");
      if (surface === "preview") await page.getByRole("button", { name: "Notifications", exact: true }).click();
      const scope = surface === "preview" ? page.getByRole("region", { name: "Notifications", exact: true }) : page.getByRole("main");
      await scope.getByRole("link", { name: surface === "preview" ? /Linked notification/ : "Open", exact: surface === "inbox" }).click();
      await expect(page).toHaveURL(new RegExp(destination.split("?")[0]));
      if (destination.startsWith("/orders")) await expect(page.getByRole("main")).toContainText(/permission|access denied/i);
      else await expect(page.getByRole("heading", { name: /page not found/i })).toBeVisible();
      expect(calls).toEqual([]);
    });
  }

  test(`${surface} rejects unsafe links and isolates external HTTP links`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions });
    await installAdminShellMocks(page, permissions);
    const unsafe = ["javascript:alert(1)", "data:text/html,test", "//example.com", "/\\example.com", " https://example.com", "https://user:password@example.com", "relative-path"];
    await page.route("**/api/v1/notifications/?*", route => route.fulfill({ json: [
      ...unsafe.map((link, index) => ({ ...entry, id: `unsafe-${index}`, title: `Unsafe ${index}`, link })),
      { ...entry, link: "https://example.com/help" },
    ] }));
    await page.goto("/notifications");
    if (surface === "preview") await page.getByRole("button", { name: "Notifications", exact: true }).click();
    const scope = surface === "preview" ? page.getByRole("region", { name: "Notifications", exact: true }) : page.getByRole("main");
    await expect(scope.getByText("Unsafe 0", { exact: true })).toBeVisible();
    const links = scope.locator("li a");
    await expect(links).toHaveCount(1);
    await expect(links).toHaveAttribute("href", "https://example.com/help");
    await expect(links).toHaveAttribute("target", "_blank");
    await expect(links).toHaveAttribute("rel", "noopener noreferrer");
  });
}
