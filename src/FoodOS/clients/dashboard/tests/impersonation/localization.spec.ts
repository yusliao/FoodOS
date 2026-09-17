import { expect, test, type Page } from "@playwright/test";
import { installShellMocks } from "../helpers/shell-mocks";

async function setup(page: Page, culture: string) {
  await page.setViewportSize({ width: 390, height: 844 });
  await installShellMocks(page);
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  const token = `${encode({ alg: "HS256" })}.${encode({
    sub: "customer-user", name: "LongCustomerName".repeat(8), tenant: "customer-" + "a".repeat(55),
    act_sub: "operator-user", act_tenant: "root", business_actor: "customer",
    exp: Math.floor(Date.now() / 1000) + 3600,
  })}.test-signature`;
  await page.addInitScript(({ token, culture }) => {
    localStorage.setItem("fsh.dashboard.accessToken", token);
    localStorage.removeItem("fsh.dashboard.refreshToken");
    localStorage.setItem("foodos.culture", culture);
  }, { token, culture });
}

for (const culture of ["en-US", "zh-CN"]) {
  const zh = culture === "zh-CN";
  test(`${culture} mobile banner fits long identities and distinguishes local exit from remote revocation`, async ({ page }) => {
    await setup(page, culture);
    await page.route("**/api/v1/identity/impersonation/end", route => route.fulfill({ status: 500, json: {} }));
    await page.goto("/settings/profile");
    const banner = page.getByRole("status", { name: zh ? "模拟登录会话" : "Impersonation session", exact: true });
    await expect(banner).toBeVisible();
    await expect(banner.getByText(zh ? "跨客户模拟登录" : "Cross-tenant impersonation", { exact: true })).toBeVisible();
    const end = banner.getByRole("button", { name: zh ? "结束模拟登录" : "End impersonation", exact: true });
    await expect(end).toBeInViewport();
    expect(await banner.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await end.click();
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByText(zh ? "已退出本门户会话" : "Signed out of this portal session", { exact: true })).toBeVisible();
    await expect(page.getByText(zh ? /尚未确认远端授权已撤销/ : /Remote revocation is not confirmed/)).toBeVisible();
  });

  test(`${culture} mobile terminal page is localized and clears the expired session`, async ({ page }) => {
    await setup(page, culture);
    await page.goto("/impersonation-ended");
    await expect(page.getByRole("heading", { name: zh ? "模拟登录已结束" : "Impersonation ended", exact: true })).toBeVisible();
    await expect(page.getByText(zh ? /访问权限已被撤销或已过期/ : /revoked or has expired/)).toBeVisible();
    await page.getByRole("button", { name: zh ? "返回登录" : "Back to sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect(await page.evaluate(() => localStorage.getItem("fsh.dashboard.accessToken"))).toBeNull();
  });
}
