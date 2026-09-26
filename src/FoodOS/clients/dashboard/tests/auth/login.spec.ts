import { expect, test } from "@playwright/test";
import { mockJsonResponse, mockProblemDetails } from "../helpers/api-mocks";
import { installShellMocks } from "../helpers/shell-mocks";

// The dashboard login page (rebuilt to the dentalOS card layout): 东方味力 logo
// lockup + tenant workspace caption, tenant/email/password card, and a
// demoMode-gated restaurant account picker that signs in instantly.

function token(tenant = "acme", subject = "customer-1") {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return [encode({ alg: "HS256" }), encode({ sub: subject, tenant, business_actor: "customer", exp: Math.floor(Date.now() / 1000) + 3600 }), "sig"].join(".");
}

const TOKEN_RESPONSE = {
  accessToken: token(),
  refreshToken: "refresh",
  accessTokenExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  refreshTokenExpiresAt: new Date(Date.now() + 7_200_000).toISOString(),
};

/** Force the runtime config so demoMode is deterministic per test. */
async function setConfig(page: import("@playwright/test").Page, demoMode: boolean) {
  await page.route("**/config.json", (route) =>
    route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ apiBase: "", defaultTenant: "acme", demoMode }),
    }),
  );
}

test.describe("login — page chrome", () => {
  test.beforeEach(async ({ page }) => {
    await setConfig(page, true);
  });

  test("renders the 东方味力 logo lockup with the workspace caption", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("img", { name: "东方味力" })).toBeVisible();
    await expect(page.getByText(/tenant workspace/i)).toBeVisible();
    await expect(page.getByRole("heading", { name: /welcome back/i })).toBeVisible();
    await expect(page.getByText(/sign in to your account/i)).toBeVisible();
  });

  test("renders the tenant + email + password fields", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByLabel("Tenant")).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: /forgot/i })).toHaveAttribute("href", "/forgot-password");
  });

  test("password visibility toggle flips the input type", async ({ page }) => {
    await page.goto("/login");
    const pwd = page.getByLabel("Password", { exact: true });
    await expect(pwd).toHaveAttribute("type", "password");
    await page.getByRole("button", { name: /show password/i }).click();
    await expect(pwd).toHaveAttribute("type", "text");
    await page.getByRole("button", { name: /hide password/i }).click();
    await expect(pwd).toHaveAttribute("type", "password");
  });

  test("submit is disabled until tenant + email + password are filled", async ({ page }) => {
    await page.goto("/login");
    const submit = page.getByRole("button", { name: /^sign in$/i });
    // Tenant defaults to the configured restaurant demo tenant; fill the rest to enable.
    await expect(submit).toBeDisabled();
    await page.getByLabel("Email").fill("alice@acme.com");
    await page.getByLabel("Password", { exact: true }).fill("secret123");
    await expect(submit).toBeEnabled();
  });
});

test.describe("login — manual sign in", () => {
  test.beforeEach(async ({ page }) => {
    await setConfig(page, true);
    await mockJsonResponse(page, "**/api/v1/identity/token/issue", TOKEN_RESPONSE);
  });

  test("POSTs credentials with the tenant header", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Tenant").fill("acme");
    await page.getByLabel("Email").fill("alice@acme.com");
    await page.getByLabel("Password", { exact: true }).fill("Password123!");

    const reqPromise = page.waitForRequest(
      (r) => r.url().includes("/api/v1/identity/token/issue") && r.method() === "POST",
    );
    await page.getByRole("button", { name: /^sign in$/i }).click();
    const req = await reqPromise;

    expect(req.headers().tenant).toBe("acme");
    expect(JSON.parse(req.postData() ?? "{}")).toMatchObject({
      email: "alice@acme.com",
      password: "Password123!",
    });
  });

  test("surfaces a server error without leaving the page", async ({ page }) => {
    await mockProblemDetails(page, "**/api/v1/identity/token/issue", 401, {
      title: "Unauthorized",
      detail: "Invalid credentials.",
    });
    await page.goto("/login");
    await page.getByLabel("Email").fill("alice@acme.com");
    await page.getByLabel("Password", { exact: true }).fill("wrongpw");
    await page.getByRole("button", { name: /^sign in$/i }).click();

    await expect(page.getByRole("alert")).toContainText(/invalid credentials/i);
    await expect(page.getByRole("heading", { name: /welcome back/i })).toBeVisible();
  });

  test("prompts for an authenticator code and forwards retries without clearing scope", async ({ page }) => {
    const bodies: Array<Record<string, unknown>> = [];
    await page.unroute("**/api/v1/identity/token/issue");
    await page.route("**/api/v1/identity/token/issue", async (route) => {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      bodies.push(body);
      if (bodies.length === 1) {
        await route.fulfill({
          status: 401,
          contentType: "application/problem+json",
          json: { status: 401, title: "Unauthorized", detail: "two_factor_required: An authenticator code is required to complete sign-in." },
        });
        return;
      }
      await route.fulfill({
        status: 401,
        contentType: "application/problem+json",
        json: { status: 401, title: "Unauthorized", detail: "two_factor_invalid: The authenticator code is invalid or expired." },
      });
    });

    await page.goto("/login");
    await page.getByLabel("Tenant").fill("acme");
    await page.getByLabel("Email").fill("alice@acme.com");
    await page.getByLabel("Password", { exact: true }).fill("Password123!");
    await page.getByRole("button", { name: /^sign in$/i }).click();

    const code = page.getByLabel("Authenticator code");
    await expect(code).toBeVisible();
    await expect(page.getByRole("button", { name: /^sign in$/i })).toBeDisabled();
    await expect(page.getByLabel("Tenant")).toHaveValue("acme");
    await expect(page.getByLabel("Email")).toHaveValue("alice@acme.com");

    await code.fill("654 321");
    await expect(code).toHaveValue("654321");
    await page.getByRole("button", { name: /^sign in$/i }).click();

    await expect(page.getByRole("alert")).toContainText(/invalid or expired/i);
    await expect(code).toHaveValue("654321");
    expect(bodies).toHaveLength(2);
    expect(bodies[0]).not.toHaveProperty("twoFactorCode");
    expect(bodies[1]).toMatchObject({
      email: "alice@acme.com",
      password: "Password123!",
      twoFactorCode: "654321",
    });
  });
});

test.describe("login — demo account picker", () => {
  test("the demo button is hidden when demoMode is off", async ({ page }) => {
    await setConfig(page, false);
    await page.goto("/login");
    await expect(page.getByRole("button", { name: /sign in with a demo account/i })).toHaveCount(0);
  });

  test("opens the restaurant account dialog without advertising root", async ({ page }) => {
    await setConfig(page, true);
    await page.goto("/login");
    await page.getByRole("button", { name: /sign in with a demo account/i }).click();

    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("heading", { name: /choose a restaurant account/i })).toBeVisible();
    await expect(dialog.getByText(/live demo/i)).toBeVisible();
    // Tenant rail — scope to the nav so we don't collide with user rows.
    const rail = dialog.getByRole("navigation", { name: /demo tenants/i });
    await expect(rail.getByRole("button", { name: /root/i })).toHaveCount(0);
    await expect(rail.getByRole("button", { name: /acme corp/i })).toBeVisible();
    await expect(rail.getByRole("button", { name: /globex/i })).toBeVisible();
  });

  test("switching tenant swaps the user list", async ({ page }) => {
    await setConfig(page, true);
    await page.goto("/login");
    await page.getByRole("button", { name: /sign in with a demo account/i }).click();
    const dialog = page.getByRole("dialog");
    const rail = dialog.getByRole("navigation", { name: /demo tenants/i });

    // Acme is the first restaurant and active by default.
    await expect(dialog.getByText("admin@acme.com")).toBeVisible();
    await rail.getByRole("button", { name: /globex/i }).click();
    await expect(dialog.getByText("admin@globex.com")).toBeVisible();
  });

  test("tapping a demo user signs in instantly with that account's tenant", async ({ page }) => {
    await setConfig(page, true);
    await page.addInitScript(() => localStorage.setItem("foodos.shop.storeId", "previous-account-store"));
    await installShellMocks(page);
    await mockJsonResponse(page, "**/api/v1/identity/token/issue", TOKEN_RESPONSE);
    await page.goto("/login");
    await page.getByRole("button", { name: /sign in with a demo account/i }).click();
    const dialog = page.getByRole("dialog");
    const rail = dialog.getByRole("navigation", { name: /demo tenants/i });

    await rail.getByRole("button", { name: /acme corp/i }).click();

    const reqPromise = page.waitForRequest(
      (r) => r.url().includes("/api/v1/identity/token/issue") && r.method() === "POST",
    );
    await dialog.getByRole("button", { name: /admin@acme\.com/i }).click();
    const req = await reqPromise;

    expect(req.headers().tenant).toBe("acme");
    expect(JSON.parse(req.postData() ?? "{}")).toMatchObject({ email: "admin@acme.com" });
    await expect.poll(() => page.evaluate(() => localStorage.getItem("foodos.shop.storeId"))).toBeNull();
  });
});
