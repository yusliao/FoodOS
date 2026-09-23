import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";

const ACCESS_KEY = "fsh.dashboard.accessToken";
const REFRESH_KEY = "fsh.dashboard.refreshToken";
const TENANT_KEY = "fsh.dashboard.tenant";

function token({
  tenant = "acme",
  subject = "customer-1",
  expired = false,
}: {
  tenant?: string;
  subject?: string;
  expired?: boolean;
} = {}) {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return [
    encode({ alg: "HS256" }),
    encode({
      sub: subject,
      tenant,
      business_actor: tenant === "root" ? "operator" : "customer",
      exp: Math.floor(Date.now() / 1000) + (expired ? -3600 : 3600),
    }),
    "sig",
  ].join(".");
}

async function seedExpiredCustomer(page: Page) {
  await page.addInitScript(
    ({ access, accessKey, refreshKey, tenantKey }) => {
      localStorage.setItem(accessKey, access);
      localStorage.setItem(refreshKey, "customer-refresh");
      // Deliberately stale/tampered. Refresh must derive scope from the token.
      localStorage.setItem(tenantKey, "root");
    },
    {
      access: token({ expired: true }),
      accessKey: ACCESS_KEY,
      refreshKey: REFRESH_KEY,
      tenantKey: TENANT_KEY,
    },
  );
}

test("operator token returned from dashboard login is rejected and cleared", async ({ page }) => {
  await mockJsonResponse(page, "**/api/v1/identity/token/issue", {
    accessToken: token({ tenant: "root" }),
    refreshToken: "root-refresh",
  });

  await page.goto("/login");
  await page.getByLabel("Tenant").fill("acme");
  await page.getByLabel("Email").fill("operator@root.test");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();

  await expect(page.getByRole("alert")).toContainText("Operator accounts must use the admin app");
  await expect(page).toHaveURL(/\/login$/);
  expect(await page.evaluate((key) => localStorage.getItem(key), ACCESS_KEY)).toBeNull();
  expect(await page.evaluate((key) => localStorage.getItem(key), REFRESH_KEY)).toBeNull();
});

test("stored operator session cannot restore into the restaurant portal", async ({ page }) => {
  await page.addInitScript(
    ({ access, accessKey, refreshKey, tenantKey }) => {
      localStorage.setItem(accessKey, access);
      localStorage.setItem(refreshKey, "root-refresh");
      localStorage.setItem(tenantKey, "root");
    },
    {
      access: token({ tenant: "root" }),
      accessKey: ACCESS_KEY,
      refreshKey: REFRESH_KEY,
      tenantKey: TENANT_KEY,
    },
  );
  const businessRequests: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/v1/") && !request.url().includes("/token/refresh")) {
      businessRequests.push(request.url());
    }
  });

  await page.goto("/");

  await expect(page).toHaveURL(/\/login$/);
  expect(businessRequests).toEqual([]);
  expect(await page.evaluate((key) => localStorage.getItem(key), ACCESS_KEY)).toBeNull();
});

for (const [name, refreshed] of [
  ["operator identity", token({ tenant: "root" })],
  ["different customer subject", token({ subject: "customer-2" })],
  ["different customer tenant", token({ tenant: "globex" })],
] as const) {
  test(`refresh returning ${name} clears the customer session`, async ({ page }) => {
    await seedExpiredCustomer(page);
    let refreshTenant: string | undefined;
    let refreshApp: string | undefined;
    await page.route("**/api/v1/identity/token/refresh", async (route) => {
      refreshTenant = route.request().headers().tenant;
      refreshApp = route.request().headers()["x-fsh-app"];
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ token: refreshed, refreshToken: "rotated" }),
      });
    });

    await page.goto("/");

    await expect(page).toHaveURL(/\/login$/);
    expect(refreshTenant).toBe("acme");
    expect(refreshApp).toBe("dashboard");
    expect(await page.evaluate((key) => localStorage.getItem(key), ACCESS_KEY)).toBeNull();
    expect(await page.evaluate((key) => localStorage.getItem(key), REFRESH_KEY)).toBeNull();
    expect(await page.evaluate((key) => localStorage.getItem(key), TENANT_KEY)).toBeNull();
  });
}
