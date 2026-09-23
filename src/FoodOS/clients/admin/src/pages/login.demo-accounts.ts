// Admin demo accounts — mirrors src/Host/FoodOS.Api/DevSeeding (keep in sync).
// Static — no API call — because the login page is unauthenticated and the
// API can't safely advertise credentials. Admin only surfaces the root /
// operator superadmin; tenant-level demo users live in the dashboard app.

export type DemoAccount = {
  email: string;
  password: string;
  tenant: string;
  /** Short display label */
  label: string;
  /** Initials rendered in the avatar */
  initials: string;
  /** One-line persona explainer */
  persona: string;
};

export const DEMO_PASSWORD = "Password123!";

/**
 * Root/operator accounts provisioned by DemoSeeder. The recovery account
 * admin@root.com intentionally stays out of this list because its password is
 * controlled by Seed:DefaultAdminPassword rather than the demo credential.
 */
export const ADMIN_DEMO_ACCOUNTS: DemoAccount[] = [
  {
    email: "superadmin@root.com",
    password: DEMO_PASSWORD,
    tenant: "root",
    label: "SuperAdmin",
    initials: "SA",
    persona: "Platform operator · cross-tenant control",
  },
  { email: "manager@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Manager", initials: "ML", persona: "Operator manager" },
  { email: "support@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Support", initials: "SR", persona: "Customer support" },
  { email: "purchaser@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Purchaser", initials: "PC", persona: "Procurement operator" },
  { email: "qc@root.com", password: DEMO_PASSWORD, tenant: "root", label: "QC Inspector", initials: "QD", persona: "Quality inspector" },
  { email: "whlead@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Warehouse Lead", initials: "WL", persona: "Warehouse lead" },
  { email: "picker@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Picker", initials: "PP", persona: "Warehouse picker" },
  { email: "dispatch@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Dispatcher", initials: "DC", persona: "Logistics dispatcher" },
  { email: "driver@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Driver", initials: "DN", persona: "Delivery driver" },
  { email: "finance@root.com", password: DEMO_PASSWORD, tenant: "root", label: "Finance", initials: "FO", persona: "Finance clerk" },
];
