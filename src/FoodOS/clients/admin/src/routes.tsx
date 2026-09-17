import { lazy } from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";
import { AppShell } from "@/components/layout/app-shell";
import { ProtectedRoute } from "@/auth/protected-route";
import { RouteGuard } from "@/auth/route-guard";
import { RouteError } from "@/components/route-error";
import { LoginPage } from "@/pages/login";
import { DashboardPage } from "@/pages/dashboard";
import { NotFoundPage } from "@/pages/not-found";
import {
  AuditingPermissions,
  BillingPermissions,
  CatalogPermissions,
  IdentityPermissions,
  MultitenancyPermissions,
  NotificationPermissions,
  OrderingPermissions,
  ProcurementPermissions,
  LogisticsPermissions,
  WebhooksPermissions,
} from "@/lib/permissions";

// Lazy-loaded pages — each `import()` becomes its own bundle chunk so the
// initial paint only ships the shell + dashboard. We re-export the named
// page component as the chunk's `default` via the wrapper-import trick.
const lazyNamed = <T extends string>(
  loader: () => Promise<Record<string, unknown>>,
  named: T,
) =>
  lazy(async () => {
    const mod = await loader();
    return { default: mod[named] as React.ComponentType };
  });

const TenantsListPage = lazyNamed(() => import("@/pages/tenants/list"), "TenantsListPage");
const CustomersPage = lazyNamed(() => import("@/pages/customers/list"), "CustomersPage");
const StoresPage = lazyNamed(() => import("@/pages/customers/stores"), "StoresPage");
const BrandsPage = lazyNamed(() => import("@/pages/catalog/brands"), "BrandsPage");
const CategoriesPage = lazyNamed(() => import("@/pages/catalog/categories"), "CategoriesPage");
const ProductsPage = lazyNamed(() => import("@/pages/catalog/products"), "ProductsPage");
const PricingPage = lazyNamed(() => import("@/pages/catalog/pricing"), "PricingPage");
const OrdersPage = lazyNamed(() => import("@/pages/orders/orders"), "OrdersPage");
const SuppliersPage = lazyNamed(() => import("@/pages/procurement/suppliers"), "SuppliersPage");
const VehiclesPage = lazyNamed(() => import("@/pages/logistics/vehicles"), "VehiclesPage");
const DriversPage = lazyNamed(() => import("@/pages/logistics/drivers"), "DriversPage");
const DeliveryRoutesPage = lazyNamed(() => import("@/pages/logistics/routes"), "DeliveryRoutesPage");
const PurchaseOrdersPage = lazyNamed(() => import("@/pages/procurement/purchase-orders"), "PurchaseOrdersPage");
const OrderDetailPage = lazyNamed(() => import("@/pages/orders/orders"), "OrderDetailPage");
const TenantDetailPage = lazyNamed(() => import("@/pages/tenants/detail"), "TenantDetailPage");
const UsersListPage = lazyNamed(() => import("@/pages/users/list"), "UsersListPage");
const UserDetailPage = lazyNamed(() => import("@/pages/users/detail"), "UserDetailPage");
const RolesListPage = lazyNamed(() => import("@/pages/roles/list"), "RolesListPage");
const RoleDetailPage = lazyNamed(() => import("@/pages/roles/detail"), "RoleDetailPage");
const BillingLayout = lazyNamed(() => import("@/pages/billing/layout"), "BillingLayout");
const PlansListPage = lazyNamed(() => import("@/pages/billing/plans-list"), "PlansListPage");
const InvoicesListPage = lazyNamed(() => import("@/pages/billing/invoices-list"), "InvoicesListPage");
const InvoiceDetailPage = lazyNamed(() => import("@/pages/billing/invoice-detail"), "InvoiceDetailPage");
const AuditsListPage = lazyNamed(() => import("@/pages/audits/list"), "AuditsListPage");
const HealthPage = lazyNamed(() => import("@/pages/health/page"), "HealthPage");
const ImpersonationListPage = lazyNamed(() => import("@/pages/impersonation/list"), "ImpersonationListPage");
const WebhooksListPage = lazyNamed(() => import("@/pages/webhooks/list"), "WebhooksListPage");
const WebhookDetailPage = lazyNamed(() => import("@/pages/webhooks/detail"), "WebhookDetailPage");
const NotificationsInboxPage = lazyNamed(() => import("@/pages/notifications/inbox"), "NotificationsInboxPage");
const SettingsLayout = lazyNamed(() => import("@/pages/settings/layout"), "SettingsLayout");
const ProfileSettings = lazyNamed(() => import("@/pages/settings/profile"), "ProfileSettings");
const SecuritySettings = lazyNamed(() => import("@/pages/settings/security"), "SecuritySettings");
const SessionsSettings = lazyNamed(() => import("@/pages/settings/sessions"), "SessionsSettings");
const AppearanceSettings = lazyNamed(() => import("@/pages/settings/appearance"), "AppearanceSettings");
const ForgotPasswordPage = lazyNamed(
  () => import("@/pages/auth/forgot-password"),
  "ForgotPasswordPage",
);
const ResetPasswordPage = lazyNamed(
  () => import("@/pages/auth/reset-password"),
  "ResetPasswordPage",
);
const ConfirmEmailPage = lazyNamed(
  () => import("@/pages/auth/confirm-email"),
  "ConfirmEmailPage",
);

// Each route's element is wrapped in RouteGuard with the same permissions the
// server endpoint requires, so the UI mirrors server-side authorization. Auth
// itself is enforced one layer up by <ProtectedRoute />.

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage />, errorElement: <RouteError /> },
  { path: "/forgot-password", element: <ForgotPasswordPage />, errorElement: <RouteError /> },
  { path: "/reset-password", element: <ResetPasswordPage />, errorElement: <RouteError /> },
  { path: "/confirm-email", element: <ConfirmEmailPage />, errorElement: <RouteError /> },
  {
    element: <ProtectedRoute />,
    errorElement: <RouteError />,
    children: [
      {
        element: <AppShell />,
        errorElement: <RouteError />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: "procurement/purchase-orders", element: <RouteGuard perms={[ProcurementPermissions.Purchase.View]}><PurchaseOrdersPage /></RouteGuard> },
          { path: "procurement/suppliers", element: <RouteGuard perms={[ProcurementPermissions.Suppliers.View]}><SuppliersPage /></RouteGuard> },
          { path: "logistics/vehicles", element: <RouteGuard perms={[LogisticsPermissions.Vehicles.View]}><VehiclesPage /></RouteGuard> },
          { path: "logistics/drivers", element: <RouteGuard perms={[LogisticsPermissions.Drivers.View]}><DriversPage /></RouteGuard> },
          { path: "logistics/routes", element: <RouteGuard perms={[LogisticsPermissions.Routes.View]}><DeliveryRoutesPage /></RouteGuard> },
          { path: "orders", element: <RouteGuard perms={[OrderingPermissions.Orders.View]}><OrdersPage /></RouteGuard> },
          { path: "orders/:id", element: <RouteGuard perms={[OrderingPermissions.Orders.View]}><OrderDetailPage /></RouteGuard> },
          { path: "customers", element: <RouteGuard perms={[OrderingPermissions.Customers.View]}><CustomersPage /></RouteGuard> },
          { path: "stores", element: <RouteGuard perms={[OrderingPermissions.Stores.View]}><StoresPage /></RouteGuard> },
          { path: "catalog", element: <Navigate to="/catalog/products" replace /> },
          { path: "catalog/products", element: <RouteGuard perms={[CatalogPermissions.Products.View]}><ProductsPage /></RouteGuard> },
          { path: "catalog/brands", element: <RouteGuard perms={[CatalogPermissions.Brands.View]}><BrandsPage /></RouteGuard> },
          { path: "catalog/categories", element: <RouteGuard perms={[CatalogPermissions.Categories.View]}><CategoriesPage /></RouteGuard> },
          { path: "catalog/pricing", element: <RouteGuard perms={[CatalogPermissions.PriceLists.View]}><PricingPage /></RouteGuard> },

          // Tenants — root-only
          {
            path: "tenants",
            element: (
              <RouteGuard perms={[MultitenancyPermissions.Tenants.View]}>
                <TenantsListPage />
              </RouteGuard>
            ),
          },
          {
            // /tenants/new — creation is now a dialog on the list page.
            // Redirect any bookmarked links back to /tenants.
            path: "tenants/new",
            element: <Navigate to="/tenants" replace />,
          },
          {
            path: "tenants/:id",
            element: (
              <RouteGuard perms={[MultitenancyPermissions.Tenants.View]}>
                <TenantDetailPage />
              </RouteGuard>
            ),
          },

          // Users
          {
            path: "users",
            element: (
              <RouteGuard perms={[IdentityPermissions.Users.View]}>
                <UsersListPage />
              </RouteGuard>
            ),
          },
          {
            // /users/new — creation is now a dialog on the list page.
            // Redirect any bookmarked links back to /users.
            path: "users/new",
            element: <Navigate to="/users" replace />,
          },
          {
            path: "users/:id",
            element: (
              <RouteGuard perms={[IdentityPermissions.Users.View]}>
                <UserDetailPage />
              </RouteGuard>
            ),
          },

          // Roles
          {
            path: "roles",
            element: (
              <RouteGuard perms={[IdentityPermissions.Roles.View]}>
                <RolesListPage />
              </RouteGuard>
            ),
          },
          {
            // /roles/new — creation is now a dialog on the list page.
            // Redirect any bookmarked links back to /roles.
            path: "roles/new",
            element: <Navigate to="/roles" replace />,
          },
          {
            path: "roles/:id",
            element: (
              <RouteGuard perms={[IdentityPermissions.Roles.View]}>
                <RoleDetailPage />
              </RouteGuard>
            ),
          },

          // Billing
          {
            path: "billing",
            element: (
              <RouteGuard perms={[BillingPermissions.View]}>
                <BillingLayout />
              </RouteGuard>
            ),
            children: [
              { index: true, element: <Navigate to="/billing/invoices" replace /> },
              { path: "plans", element: <PlansListPage /> },
              { path: "invoices", element: <InvoicesListPage /> },
              { path: "invoices/:invoiceId", element: <InvoiceDetailPage /> },
            ],
          },

          // Impersonation
          {
            path: "impersonation",
            element: (
              <RouteGuard perms={[IdentityPermissions.Impersonation.View]}>
                <ImpersonationListPage />
              </RouteGuard>
            ),
          },

          // Audits — detail opens as a side sheet on the list page.
          // Redirect any bookmarked /audits/:id links back to /audits.
          {
            path: "audits",
            element: (
              <RouteGuard perms={[AuditingPermissions.AuditTrails.View]}>
                <AuditsListPage />
              </RouteGuard>
            ),
          },
          {
            path: "audits/:id",
            element: <Navigate to="/audits" replace />,
          },

          // Webhooks — list/detail both read subscriptions, which the server
          // gates on Webhooks.View (granted to Basic by default).
          {
            path: "webhooks",
            element: (
              <RouteGuard perms={[WebhooksPermissions.Subscriptions.View]}>
                <WebhooksListPage />
              </RouteGuard>
            ),
          },
          {
            path: "webhooks/:id",
            element: (
              <RouteGuard perms={[WebhooksPermissions.Subscriptions.View]}>
                <WebhookDetailPage />
              </RouteGuard>
            ),
          },

          // Inbox uses the same view permission as the server and the navigation.
          { path: "notifications", element: <RouteGuard perms={[NotificationPermissions.Inbox.View]}><NotificationsInboxPage /></RouteGuard> },

          // Public probes remain server-public; their admin UI lives under system administration.
          { path: "health", element: <RouteGuard perms={[MultitenancyPermissions.Tenants.View]}><HealthPage /></RouteGuard> },

          // Settings — account-scoped; any signed-in user can manage their own profile + sessions + 2FA
          {
            path: "settings",
            element: <SettingsLayout />,
            children: [
              { index: true, element: <Navigate to="/settings/profile" replace /> },
              { path: "profile", element: <ProfileSettings /> },
              { path: "security", element: <SecuritySettings /> },
              { path: "sessions", element: <SessionsSettings /> },
              { path: "appearance", element: <AppearanceSettings /> },
            ],
          },
        ],
      },
    ],
  },
  { path: "*", element: <NotFoundPage /> },
]);
