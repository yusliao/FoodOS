/**
 * Permission strings + catalog mirrored from the server's *Permissions.cs
 * registries. Kept here so:
 *   1. Route guards stay typo-proof (`IdentityPermissions.Users.View`).
 *   2. The Role editor can render every assignable permission grouped
 *      by category without an extra round-trip.
 *
 * If the server registry adds permissions, mirror them here — there is no
 * runtime fetch (no /permissions catalog endpoint exists).
 * Convention follows the server: `Permissions.{Resource}.{Action}`.
 */

export const FilesPermissions = Object.freeze({ Upload: "Permissions.Files.Upload", DeleteOwn: "Permissions.Files.DeleteOwn" });
export const TicketsPermissions = Object.freeze({ Delete: "Permissions.Tickets.Delete", Restore: "Permissions.Tickets.Restore", Create: "Permissions.Tickets.Create", Update: "Permissions.Tickets.Update", View: "Permissions.Tickets.View", Comment: "Permissions.Tickets.Comment", Assign: "Permissions.Tickets.Assign", Resolve: "Permissions.Tickets.Resolve", Reopen: "Permissions.Tickets.Reopen", Close: "Permissions.Tickets.Close" });

export const OrderingPermissions = Object.freeze({
  Orders: {
    View: "Permissions.Ordering.Orders.View",
    Manage: "Permissions.Ordering.Orders.Manage",
    Reconcile: "Permissions.Ordering.Orders.Reconcile",
  },
  Customers: {
    View: "Permissions.Ordering.Customers.View",
    Create: "Permissions.Ordering.Customers.Create",
  },
  Stores: {
    View: "Permissions.Ordering.Stores.View",
    Create: "Permissions.Ordering.Stores.Create",
  },
});

export const LogisticsPermissions = Object.freeze({
  Routes: { View: "Permissions.Logistics.Routes.View", Create: "Permissions.Logistics.Routes.Create" },
  Drivers: { View: "Permissions.Logistics.Drivers.View", Create: "Permissions.Logistics.Drivers.Create" },
  Vehicles: { View: "Permissions.Logistics.Vehicles.View", Create: "Permissions.Logistics.Vehicles.Create" },
});

export const ProcurementPermissions = Object.freeze({
  Quality: { Pass: "Permissions.Procurement.Quality.Pass", Fail: "Permissions.Procurement.Quality.Fail", View: "Permissions.Procurement.Quality.View" },
  Purchase: { View: "Permissions.Procurement.Purchase.View", Create: "Permissions.Procurement.Purchase.Create" },
  Suppliers: {
    View: "Permissions.Procurement.Suppliers.View",
    Create: "Permissions.Procurement.Suppliers.Create",
  },
} as const);

export const InventoryPermissions = Object.freeze({
  Warehouses: { View: "Permissions.Inventory.Warehouses.View" },
});

export const CatalogPermissions = Object.freeze({
  Brands: {
    View: "Permissions.Catalog.Brands.View", Create: "Permissions.Catalog.Brands.Create",
    Update: "Permissions.Catalog.Brands.Update", Delete: "Permissions.Catalog.Brands.Delete",
  },
  Categories: {
    View: "Permissions.Catalog.Categories.View", Create: "Permissions.Catalog.Categories.Create",
    Update: "Permissions.Catalog.Categories.Update", Delete: "Permissions.Catalog.Categories.Delete",
  },
  Products: {
    View: "Permissions.Catalog.Products.View", Create: "Permissions.Catalog.Products.Create",
    Update: "Permissions.Catalog.Products.Update", Delete: "Permissions.Catalog.Products.Delete",
  },
  PriceLists: {
    View: "Permissions.Catalog.PriceLists.View", Create: "Permissions.Catalog.PriceLists.Create",
    Update: "Permissions.Catalog.PriceLists.Update",
  },
} as const);

export const IdentityPermissions = Object.freeze({
  Users: {
    View: "Permissions.Users.View",
    Search: "Permissions.Users.Search",
    Create: "Permissions.Users.Create",
    Update: "Permissions.Users.Update",
    Delete: "Permissions.Users.Delete",
    Export: "Permissions.Users.Export",
    ManageRoles: "Permissions.Users.ManageRoles",
    Impersonate: "Permissions.Users.Impersonate",
  },
  UserRoles: {
    View: "Permissions.UserRoles.View",
    Update: "Permissions.UserRoles.Update",
  },
  Roles: {
    View: "Permissions.Roles.View",
    Create: "Permissions.Roles.Create",
    Update: "Permissions.Roles.Update",
    Delete: "Permissions.Roles.Delete",
  },
  RoleClaims: {
    View: "Permissions.RoleClaims.View",
    Update: "Permissions.RoleClaims.Update",
  },
  Sessions: {
    View: "Permissions.Sessions.View",
    Revoke: "Permissions.Sessions.Revoke",
    ViewAll: "Permissions.Sessions.ViewAll",
    RevokeAll: "Permissions.Sessions.RevokeAll",
  },
  Impersonation: {
    View: "Permissions.Impersonation.View",
    Revoke: "Permissions.Impersonation.Revoke",
  },
} as const);

export const MultitenancyPermissions = Object.freeze({
  Tenants: {
    ViewTheme: "Permissions.Tenants.ViewTheme",
    UpdateTheme: "Permissions.Tenants.UpdateTheme",
    View: "Permissions.Tenants.View",
    Create: "Permissions.Tenants.Create",
    Update: "Permissions.Tenants.Update",
    UpgradeSubscription: "Permissions.Tenants.UpgradeSubscription",
  },
} as const);

export const BillingPermissions = Object.freeze({
  View: "Permissions.Billing.View",
  Manage: "Permissions.Billing.Manage",
} as const);

export const AuditingPermissions = Object.freeze({
  AuditTrails: {
    View: "Permissions.AuditTrails.View",
    ViewCrossTenant: "Permissions.AuditTrails.ViewCrossTenant",
  },
} as const);

export const WebhooksPermissions = Object.freeze({
  Subscriptions: {
    View: "Permissions.Webhooks.View",
    Create: "Permissions.Webhooks.Create",
    Delete: "Permissions.Webhooks.Delete",
    Test: "Permissions.Webhooks.Test",
  },
} as const);

export const OpsPermissions = Object.freeze({
  Kpis: {
    View: "Permissions.Ops.Kpis.View",
  },
  Trace: {
    View: "Permissions.Ops.Trace.View",
  },
} as const);

export const NotificationPermissions = Object.freeze({
  Inbox: {
    View: "Permissions.Notifications.Inbox.View",
    MarkRead: "Permissions.Notifications.Inbox.MarkRead",
  },
} as const);

// ─── Catalog (drives the Role editor) ───────────────────────────────────

export type PermissionEntry = {
  name: string;
  description: string;
  /** Only assignable on root-tenant (cross-tenant) roles. */
  root?: boolean;
  /** Granted by default to authenticated users via the basic role. */
  basic?: boolean;
};

export type PermissionGroup = {
  /** UI-facing category name. */
  category: string;
  /** Section blurb shown under the heading. */
  blurb: string;
  entries: PermissionEntry[];
};

export const PERMISSION_CATALOG: readonly PermissionGroup[] = [
  { category: "Tickets", blurb: "Read support tickets and reply.", entries: [
    { name: TicketsPermissions.View, description: "View tickets", basic: true },
    { name: TicketsPermissions.Comment, description: "Reply to tickets" },
    { name: TicketsPermissions.Delete, description: "Soft-delete tickets" },
    { name: TicketsPermissions.Restore, description: "Restore tickets" },
    { name: TicketsPermissions.Create, description: "Create tickets" },
    { name: TicketsPermissions.Update, description: "Edit tickets" },
    { name: TicketsPermissions.Assign, description: "Assign tickets" },
    { name: TicketsPermissions.Resolve, description: "Resolve tickets" },
    { name: TicketsPermissions.Reopen, description: "Reopen tickets" },
    { name: TicketsPermissions.Close, description: "Close tickets" },
  ] },
  {
    category: "Delivery records",
    blurb: "Read and register operator delivery vehicles and drivers.",
    entries: [
      { name: LogisticsPermissions.Vehicles.View, description: "View vehicles" },
      { name: LogisticsPermissions.Vehicles.Create, description: "Register vehicles" },
      { name: LogisticsPermissions.Drivers.View, description: "View drivers" },
      { name: LogisticsPermissions.Drivers.Create, description: "Register drivers" },
      { name: LogisticsPermissions.Routes.View, description: "View routes" },
      { name: LogisticsPermissions.Routes.Create, description: "Register routes" },
    ],
  },
  {
    category: "Procurement",
    blurb: "Manage operator suppliers and procurement records.",
    entries: [
      { name: ProcurementPermissions.Quality.View, description: "View quality checks" },
      { name: ProcurementPermissions.Quality.Pass, description: "Pass quality checks" },
      { name: ProcurementPermissions.Quality.Fail, description: "Fail quality checks" },
      { name: ProcurementPermissions.Purchase.View, description: "View purchase orders" },
      { name: ProcurementPermissions.Purchase.Create, description: "Create, send and appoint purchase orders" },
      { name: ProcurementPermissions.Suppliers.View, description: "View suppliers" },
      { name: ProcurementPermissions.Suppliers.Create, description: "Create suppliers" },
    ],
  },
  {
    category: "Ordering",
    blurb: "View, manage, and reconcile operator orders.",
    entries: [
      { name: OrderingPermissions.Orders.View, description: "View orders and after-sales claims" },
      { name: OrderingPermissions.Orders.Manage, description: "Manage orders and register after-sales claims" },
      { name: OrderingPermissions.Orders.Reconcile, description: "Reconcile received orders" },
    ],
  },
  {
    category: "Catalog",
    blurb: "Maintain operator-owned brands, categories, products, and pricing.",
    entries: [
      { name: CatalogPermissions.Brands.View, description: "View brands", basic: true },
      { name: CatalogPermissions.Brands.Create, description: "Create brands" },
      { name: CatalogPermissions.Brands.Update, description: "Update brands" },
      { name: CatalogPermissions.Brands.Delete, description: "Delete brands" },
      { name: CatalogPermissions.Categories.View, description: "View categories", basic: true },
      { name: CatalogPermissions.Categories.Create, description: "Create categories" },
      { name: CatalogPermissions.Categories.Update, description: "Update categories" },
      { name: CatalogPermissions.Categories.Delete, description: "Delete categories" },
      { name: CatalogPermissions.Products.View, description: "View products", basic: true },
      { name: CatalogPermissions.Products.Create, description: "Create products" },
      { name: CatalogPermissions.Products.Update, description: "Update products and base list prices" },
      { name: CatalogPermissions.Products.Delete, description: "Delete products" },
      { name: CatalogPermissions.PriceLists.View, description: "View price lists" },
      { name: CatalogPermissions.PriceLists.Create, description: "Create price lists" },
      { name: CatalogPermissions.PriceLists.Update, description: "Update price tiers and customer price locks" },
    ],
  },
  {
    category: "Tenants",
    blurb: "Provision and operate tenants. Reserved for the root-tenant operator.",
    entries: [
      { name: MultitenancyPermissions.Tenants.View, description: "View tenants", root: true },
      { name: MultitenancyPermissions.Tenants.Create, description: "Create tenants", root: true },
      { name: MultitenancyPermissions.Tenants.Update, description: "Update tenants", root: true },
      { name: MultitenancyPermissions.Tenants.UpgradeSubscription, description: "Upgrade tenant subscription", root: true },
      { name: MultitenancyPermissions.Tenants.ViewTheme, description: "View tenant theme", basic: true },
      { name: MultitenancyPermissions.Tenants.UpdateTheme, description: "Update tenant theme" },
    ],
  },
  {
    category: "Users",
    blurb: "Manage tenant user accounts and their assigned roles.",
    entries: [
      { name: IdentityPermissions.Users.View, description: "View users", basic: true },
      { name: IdentityPermissions.Users.Search, description: "Search users" },
      { name: IdentityPermissions.Users.Create, description: "Create users" },
      { name: IdentityPermissions.Users.Update, description: "Update users" },
      { name: IdentityPermissions.Users.Delete, description: "Delete users" },
      { name: IdentityPermissions.Users.Export, description: "Export users" },
      { name: IdentityPermissions.Users.ManageRoles, description: "Assign roles to users" },
      { name: IdentityPermissions.Users.Impersonate, description: "Impersonate another user" },
    ],
  },
  {
    category: "Roles",
    blurb: "Manage role definitions and their permission grants.",
    entries: [
      { name: IdentityPermissions.Roles.View, description: "View roles", basic: true },
      { name: IdentityPermissions.Roles.Create, description: "Create roles" },
      { name: IdentityPermissions.Roles.Update, description: "Update roles" },
      { name: IdentityPermissions.Roles.Delete, description: "Delete roles" },
      { name: IdentityPermissions.RoleClaims.View, description: "View role claims", basic: true },
      { name: IdentityPermissions.RoleClaims.Update, description: "Update role claims" },
      { name: IdentityPermissions.UserRoles.View, description: "View user-role assignments", basic: true },
      { name: IdentityPermissions.UserRoles.Update, description: "Update user-role assignments" },
    ],
  },
  {
    category: "Sessions",
    blurb: "View and revoke active sessions.",
    entries: [
      { name: IdentityPermissions.Sessions.View, description: "View my sessions", basic: true },
      { name: IdentityPermissions.Sessions.Revoke, description: "Revoke my sessions", basic: true },
      { name: IdentityPermissions.Sessions.ViewAll, description: "View all tenant sessions" },
      { name: IdentityPermissions.Sessions.RevokeAll, description: "Revoke any session" },
    ],
  },
  {
    category: "Billing",
    blurb: "Inspect and manage tenant subscriptions and invoices.",
    entries: [
      { name: BillingPermissions.View, description: "View billing", basic: true },
      { name: BillingPermissions.Manage, description: "Manage billing — plans, subscriptions, invoices" },
    ],
  },
  {
    category: "Audit trails",
    blurb: "Inspect security and entity-change audit events.",
    entries: [
      { name: AuditingPermissions.AuditTrails.View, description: "View audit trails", basic: true },
      {
        name: AuditingPermissions.AuditTrails.ViewCrossTenant,
        description: "View audit trails across tenants",
        root: true,
      },
    ],
  },
  {
    category: "Impersonation",
    blurb: "Inspect and revoke active impersonation sessions. Revocation invalidates the issued token immediately.",
    entries: [
      { name: IdentityPermissions.Impersonation.View, description: "View impersonation grants" },
      { name: IdentityPermissions.Impersonation.Revoke, description: "Revoke active impersonation grants" },
    ],
  },
  {
    category: "Webhooks",
    blurb: "Manage outbound webhook subscriptions and inspect their deliveries.",
    entries: [
      { name: WebhooksPermissions.Subscriptions.View, description: "View webhook subscriptions & deliveries", basic: true },
      { name: WebhooksPermissions.Subscriptions.Create, description: "Create webhook subscriptions" },
      { name: WebhooksPermissions.Subscriptions.Delete, description: "Delete webhook subscriptions" },
      { name: WebhooksPermissions.Subscriptions.Test, description: "Send test webhook deliveries" },
    ],
  },
  {
    category: "Operations",
    blurb: "Daily fulfillment, stockout, shrinkage, temperature board, and lot trace.",
    entries: [
      { name: OpsPermissions.Kpis.View, description: "View operations KPIs", basic: true },
      { name: OpsPermissions.Trace.View, description: "View lot trace timeline", basic: true },
    ],
  },
];

export const ALL_PERMISSION_NAMES: readonly string[] = PERMISSION_CATALOG.flatMap((g) =>
  g.entries.map((e) => e.name),
);
