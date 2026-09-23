// Mirrors src/Host/FoodOS.Api/DevSeeding/DevDataSeeder.cs (keep in sync).
// Static — no API call — because the login page is unauthenticated and the
// API can't safely advertise demo credentials anyway. The shape is hand-
// curated; if you add a demo account on the backend, add it here too.

export type DemoTier = "tenant-admin" | "basic";

export type DemoAccount = {
  email: string;
  password: string;
  tenant: string;
  tenantLabel: string;
  firstName: string;
  lastName: string;
  /** Pre-baked role for the chip in the row. */
  tier: DemoTier;
  /** One-line persona explainer shown under the name. */
  persona: string;
};

export const DEMO_PASSWORD = "Password123!";

const acme = (
  email: string,
  firstName: string,
  lastName: string,
  tier: DemoTier,
  persona: string,
): DemoAccount => ({
  email,
  password: DEMO_PASSWORD,
  tenant: "acme",
  tenantLabel: "Acme Corp",
  firstName,
  lastName,
  tier,
  persona,
});

const globex = (
  email: string,
  firstName: string,
  lastName: string,
  tier: DemoTier,
  persona: string,
): DemoAccount => ({
  email,
  password: DEMO_PASSWORD,
  tenant: "globex",
  tenantLabel: "Globex",
  firstName,
  lastName,
  tier,
  persona,
});

/**
 * Restaurant accounts only. Root/operator demo users belong to the admin app
 * and must never be advertised by the customer portal.
 */
export const DEMO_ACCOUNT_GROUPS: Array<{
  tenant: string;
  tenantLabel: string;
  blurb: string;
  accounts: DemoAccount[];
}> = [
  {
    tenant: "acme",
    tenantLabel: "Acme Corp",
    blurb: "operations · populated catalog",
    accounts: [
      acme("admin@acme.com", "Acme", "Admin", "tenant-admin", "Tenant administrator — full access"),
      acme("manager@acme.com", "Maya", "Lin", "basic", "Restaurant member"),
      acme("support@acme.com", "Sam", "Rivera", "basic", "Restaurant member"),
      acme("alice@acme.com", "Alice", "Nguyen", "basic", "Default member"),
      acme("bob@acme.com", "Bob", "Patel", "basic", "Default member"),
    ],
  },
  {
    tenant: "globex",
    tenantLabel: "Globex",
    blurb: "onboarding · sparse data",
    accounts: [
      globex("admin@globex.com", "Globex", "Admin", "tenant-admin", "Tenant administrator — full access"),
      globex("dave@globex.com", "Dave", "Hartwell", "basic", "Default member"),
    ],
  },
];
