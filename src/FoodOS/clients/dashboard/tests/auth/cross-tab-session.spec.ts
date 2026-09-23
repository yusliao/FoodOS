import { expect, test } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";

function token(sub: string, tenant = "acme", expired = false) {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${encode({ alg: "HS256" })}.${encode({ sub, tenant, name: sub, business_actor: tenant === "root" ? "operator" : "customer", exp: expired ? 1 : Math.floor(Date.now() / 1000) + 3600 })}.test-signature`;
}

for (const replacement of ["customer", "root", "expired", "logout"]) {
  test(`another same-origin tab changes session to ${replacement} without retaining old drafts or permissions`, async ({ page, context }) => {
    await installShellMocks(page);
    const writer = await context.newPage();
    await writer.goto("/config.json");
    const original = token("Alice");
    const next = replacement === "root" ? token("Operator", "root") : token("Bob", "beta", replacement === "expired");
    await writer.evaluate(original => {
      localStorage.setItem("fsh.dashboard.accessToken", original);
      localStorage.setItem("fsh.dashboard.tenant", "acme");
      localStorage.setItem("fsh.dashboard.permissions", JSON.stringify(["Permissions.Users.View"]));
      localStorage.setItem("foodos.shop.storeId", "old-store");
    }, original);
    const profileTokens: string[] = [];
    let newPermissionReads = 0;
    await page.route("**/api/v1/identity/permissions", route => {
      const isOriginal = route.request().headers().authorization === `Bearer ${original}`;
      if (!isOriginal) newPermissionReads++;
      return route.fulfill({ json: isOriginal ? ["Permissions.Users.View"] : [] });
    });
    await page.route("**/api/v1/identity/profile", route => {
      const auth = route.request().headers().authorization;
      profileTokens.push(auth);
      const firstName = auth === `Bearer ${original}` ? "Alice" : "Bob";
      return route.fulfill({ json: { id: firstName, firstName, lastName: "Customer", userName: firstName, email: `${firstName}@example.com`, isActive: true } });
    });
    await page.goto("/settings/profile");
    await expect(page.locator("#first-name")).toHaveValue("Alice");
    await page.locator("#first-name").fill("Unsaved Alice draft");
    await writer.evaluate(({ next, replacement }) => {
      if (replacement === "logout") localStorage.removeItem("fsh.dashboard.accessToken");
      else localStorage.setItem("fsh.dashboard.accessToken", next);
      localStorage.setItem("fsh.dashboard.tenant", replacement === "root" ? "root" : "beta");
    }, { next, replacement });

    if (replacement === "customer") {
      await expect(page.locator("#first-name")).toHaveValue("Bob");
      await expect.poll(() => newPermissionReads).toBeGreaterThan(0);
      expect(await page.evaluate(() => JSON.parse(localStorage.getItem("fsh.dashboard.permissions") ?? "[]"))).toEqual([]);
      expect(profileTokens).toContain(`Bearer ${next}`);
    } else {
      await expect(page).toHaveURL(/\/login$/);
      expect(await page.evaluate(() => localStorage.getItem("fsh.dashboard.accessToken"))).toBeNull();
      expect(profileTokens).not.toContain(`Bearer ${next}`);
    }
    expect(await page.evaluate(() => localStorage.getItem("foodos.shop.storeId"))).toBeNull();
    await writer.close();
  });
}
