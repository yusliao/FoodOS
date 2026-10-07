import type { Page } from "@playwright/test";

export const catalog = [
  { name: "Permissions.Users.View", description: "View users", resource: "Users", action: "View", isBasic: true, isRoot: false, isCustomer: true },
  { name: "Permissions.Users.Create", description: "Create users", resource: "Users", action: "Create", isBasic: false, isRoot: false, isCustomer: true },
  { name: "Permissions.Roles.View", description: "View roles", resource: "Roles", action: "View", isBasic: true, isRoot: false, isCustomer: true },
  { name: "Permissions.Sessions.View", description: "View my sessions", resource: "Sessions", action: "View", isBasic: true, isRoot: false, isCustomer: true },
  { name: "Permissions.Ordering.Customers.View", description: "View customers", resource: "Ordering.Customers", action: "View", isBasic: false, isRoot: false, isCustomer: false },
  { name: "Permissions.WmsIntegration.View", description: "View WMS integration", resource: "WmsIntegration", action: "View", isBasic: false, isRoot: false, isCustomer: false },
];

export async function mockPermissionCatalog(page: Page) {
  await page.route("**/api/v1/identity/permissions/catalog", route => route.fulfill({ json: catalog }));
}
