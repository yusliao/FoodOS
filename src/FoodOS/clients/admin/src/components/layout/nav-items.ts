import {
  Activity,
  BadgeDollarSign,
  Bell,
  Building2,
  FolderTree,
  LayoutDashboard,
  Package,
  Receipt,
  ScrollText,
  Settings,
  ShieldCheck,
  Tags,
  UserCog,
  UsersRound,
  Webhook,
  type LucideIcon,
} from "lucide-react";
import {
  AuditingPermissions,
  OpsPermissions,
  BillingPermissions,
  CatalogPermissions,
  NotificationPermissions,
  OrderingPermissions,
  ProcurementPermissions,
  LogisticsPermissions,
  IdentityPermissions,
  MultitenancyPermissions,
  WebhooksPermissions,
  TicketsPermissions,
  ChatPermissions,
} from "@/lib/permissions";

/** A single nav destination — label, route, icon, optional perm guard. */
export type NavSpec = {
  to: string;
  label: string;
  icon: LucideIcon;
  /** One or more permissions the user must hold to see this item. */
  perms?: readonly string[];
};

/** A collapsible section that groups related NavSpecs. */
export type NavSection = {
  id: string;
  caption: string;
  icon: LucideIcon;
  items: NavSpec[];
};

// ─── Top-level singletons ────────────────────────────────────────────────────

export const topNavTop: NavSpec[] = [
  { to: "/", label: "Overview", icon: LayoutDashboard },
];

export const topNavBottom: NavSpec[] = [
  { to: "/settings", label: "Settings", icon: Settings },
];

// ─── Section accordions ──────────────────────────────────────────────────────

export const sections: NavSection[] = [
  {
    id: "catalog", caption: "Catalog", icon: Package,
    items: [
      { to: "/catalog/products", label: "Products", icon: Package, perms: [CatalogPermissions.Products.View] },
      { to: "/catalog/brands", label: "Brands", icon: Tags, perms: [CatalogPermissions.Brands.View] },
      { to: "/catalog/categories", label: "Categories", icon: FolderTree, perms: [CatalogPermissions.Categories.View] },
      { to: "/catalog/pricing", label: "Contract pricing", icon: BadgeDollarSign, perms: [CatalogPermissions.PriceLists.View] },
    ],
  },
  {
    id: "partners", caption: "Customers and stores", icon: Building2,
    items: [
      { to: "/customers", label: "Partner customers", icon: Building2, perms: [OrderingPermissions.Customers.View] },
      { to: "/stores", label: "Restaurant stores", icon: Building2, perms: [OrderingPermissions.Stores.View] },
    ],
  },
  {
    id: "identity", caption: "Operator team", icon: UsersRound,
    items: [
      { to: "/users", label: "Employees", icon: UsersRound, perms: [IdentityPermissions.Users.View] },
      { to: "/roles", label: "Roles", icon: ShieldCheck, perms: [IdentityPermissions.Roles.View] },
    ],
  },
  {
    id: "operations", caption: "Operations", icon: Activity,
    items: [
      { to: "/reports", label: "Operations reports", icon: Activity, perms: [OpsPermissions.Kpis.View] },
      { to: "/procurement/purchase-orders", label: "Purchase orders", icon: Receipt, perms: [ProcurementPermissions.Purchase.View] },
      { to: "/procurement/suppliers", label: "Suppliers", icon: Building2, perms: [ProcurementPermissions.Suppliers.View] },
      { to: "/logistics/vehicles", label: "Vehicles", icon: Package, perms: [LogisticsPermissions.Vehicles.View] },
      { to: "/logistics/drivers", label: "Drivers", icon: UserCog, perms: [LogisticsPermissions.Drivers.View] },
      { to: "/logistics/routes", label: "Delivery routes", icon: Package, perms: [LogisticsPermissions.Routes.View] },
      { to: "/orders", label: "Orders", icon: Receipt, perms: [OrderingPermissions.Orders.View] },
      { to: "/notifications", label: "Notifications", icon: Bell, perms: [NotificationPermissions.Inbox.View] },
      { to: "/tickets", label: "Tickets", icon: ScrollText, perms: [TicketsPermissions.View] },
      { to: "/chat", label: "Chat", icon: ScrollText, perms: [ChatPermissions.View] },
      { to: "/tickets/trash", label: "Ticket trash", icon: ScrollText, perms: [TicketsPermissions.Restore] },
      { to: "/audits", label: "Audits", icon: ScrollText, perms: [AuditingPermissions.AuditTrails.View] },
    ],
  },
  {
    id: "system", caption: "System administration", icon: Settings,
    items: [
      { to: "/tenants", label: "Customer identity domains", icon: Building2, perms: [MultitenancyPermissions.Tenants.View] },
      { to: "/impersonation", label: "Impersonation", icon: UserCog, perms: [IdentityPermissions.Impersonation.View] },
      { to: "/billing", label: "Software subscriptions", icon: Receipt, perms: [BillingPermissions.View] },
      { to: "/webhooks", label: "Webhooks", icon: Webhook, perms: [WebhooksPermissions.Subscriptions.View] },
      { to: "/health", label: "Health", icon: Activity, perms: [MultitenancyPermissions.Tenants.View] },
    ],
  },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Find the section id whose items contain the given path (best prefix match). */
export function findSectionForPath(pathname: string): string | null {
  let bestId: string | null = null;
  let bestLen = 0;
  for (const s of sections) {
    for (const item of s.items) {
      if (
        (item.to === "/" && pathname === "/") ||
        (item.to !== "/" && pathname.startsWith(item.to))
      ) {
        if (item.to.length > bestLen) {
          bestLen = item.to.length;
          bestId = s.id;
        }
      }
    }
  }
  return bestId;
}

/** Returns true when the given NavSpec is the active route. */
export function isNavItemActive(item: NavSpec, pathname: string): boolean {
  if (item.to === "/") return pathname === "/";
  return pathname === item.to || pathname.startsWith(`${item.to}/`);
}

/** Filter nav items (and sections) based on granted permissions. */
export function filterNavSpec(items: NavSpec[], granted: readonly string[]): NavSpec[] {
  return items.filter((item) => {
    if (!item.perms || item.perms.length === 0) return true;
    return item.perms.every((p) => granted.includes(p));
  });
}

// ── Legacy flat export (used by sidebar-content & permission gating elsewhere) ──

/** @deprecated Use sections / topNavTop / topNavBottom instead. */
export type NavItem = NavSpec & { matchPrefix?: string };

/** @deprecated Flat list kept only for call-sites still importing NAV_ITEMS. */
export const NAV_ITEMS: NavItem[] = [...topNavTop, ...sections.flatMap(section => section.items)];

/** @deprecated Use filterNavSpec instead. */
export function filterNavItems(items: NavItem[], grantedPermissions: readonly string[]): NavItem[] {
  return items.filter((item) => {
    if (!item.perms || item.perms.length === 0) return true;
    return item.perms.every((p) => grantedPermissions.includes(p));
  });
}
