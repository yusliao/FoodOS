import { expect, test } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
import messages from "../../src/i18n/locales/en-US.json" with { type: "json" };

// Explicit expectations, independent of RouteGuard implementation. Include
// redirect aliases and inherited billing guards, not just leaf pages.
const restricted = [
  "/chat", "/chat/:channelId",
  "/tickets", "/tickets/:ticketId",
  "/tickets/trash",
  "/procurement/purchase-orders", "/procurement/suppliers",
  "/logistics/vehicles", "/logistics/drivers", "/logistics/routes",
  "/orders", "/orders/:id", "/customers", "/stores",
  "/catalog", "/catalog/products", "/catalog/brands", "/catalog/categories", "/catalog/pricing",
  "/tenants", "/tenants/new", "/tenants/:id",
  "/users", "/users/new", "/users/:id",
  "/reports", "/roles", "/roles/new", "/roles/:id",
  "/billing", "/billing/plans", "/billing/invoices", "/billing/invoices/:invoiceId",
  "/impersonation", "/audits", "/audits/:id", "/webhooks", "/webhooks/:id",
  "/notifications", "/health", "/settings/sessions",
];

// Public authentication pages, fallback, and account-scoped surfaces do not
// require business permissions. Their authentication tests live separately.
const unrestricted = [
  "/", "/login", "/forgot-password", "/reset-password", "/confirm-email", "/*",
  "/settings", "/settings/profile", "/settings/security", "/settings/appearance",
];

test("every registered admin route has an explicit permission-matrix classification", async ({ page }) => {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
  await installAdminShellMocks(page, []);
  await page.goto("/");
  await expect(page.getByRole("main")).toBeVisible();
  const actual = await page.evaluate(async () => {
    const modulePath = "/src/routes.tsx";
    const { router } = await import(/* @vite-ignore */ modulePath);
    type Route = { path?: string; index?: boolean; children?: Route[] };
    const paths = new Set<string>();
    function visit(routes: Route[], parent = "") {
      for (const route of routes) {
        const path = route.path?.startsWith("/") ? route.path : [parent, route.path].filter(Boolean).join("/");
        if (route.path || route.index) paths.add(path.startsWith("/") ? path : `/${path}`);
        if (route.children) visit(route.children, path);
      }
    }
    visit(router.routes);
    return [...paths].sort();
  });
  expect(actual).toEqual([...restricted, ...unrestricted].sort());
});

for (const routePath of restricted) {
  test(`zero-permission operator is denied without business requests: ${routePath}`, async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [] });
    await installAdminShellMocks(page, []);
    const business: string[] = [];
    const isBusiness = (url: string) => {
      const path = new URL(url).pathname;
      return path.startsWith("/health/") || (path.startsWith("/api/") &&
        path !== "/api/v1/identity/permissions" && path !== "/api/v1/identity/profile" &&
        !path.startsWith("/api/v1/realtime/"));
    };
    page.on("request", request => { if (isBusiness(request.url())) business.push(`${request.method()} ${request.url()}`); });
    // Unexpected calls are both recorded and blocked; never fall through to a
    // developer's backend merely because this test intentionally grants none.
    await page.route("**/api/**", route => isBusiness(route.request().url()) ? route.abort() : route.fallback());
    await page.route("**/health/**", route => route.abort());
    await page.goto(routePath.replace(/:[^/]+/g, "00000000-0000-0000-0000-000000000001"));
    await expect(page.getByRole("main").getByRole("heading", { name: messages.common.forbiddenTitle, exact: true })).toBeVisible();
    expect(business).toEqual([]);
  });
}
