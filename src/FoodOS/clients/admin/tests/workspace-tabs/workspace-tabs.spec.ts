import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse, mockProblemDetails } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { ADMIN_PERMS, installAdminShellMocks, paged } from "../helpers/shell-mocks";

const TABS_KEY = `foodos.admin.tabs.${TEST_USER.sub}`;

async function setup(page: Page, perms: readonly string[] = ADMIN_PERMS) {
  await seedAuthedSession(page, { ...TEST_USER, permissions: [...perms] });
  // 兜底：页面里没单独 mock 的接口一律 404，注册在最前面优先级最低。
  await mockProblemDetails(page, "**/api/v1/**", 404, { title: "Not mocked" });
  await installAdminShellMocks(page, perms);
  await mockJsonResponse(page, "**/api/v1/ops/kpis**", {});
  await mockJsonResponse(page, "**/api/v1/identity/roles{,?*}", paged([]));
  await mockJsonResponse(page, "**/api/v1/identity/users/search**", paged([]));
}

const tabBar = (page: Page) => page.getByRole("tablist", { name: "Open pages" });
const tab = (page: Page, name: string) => tabBar(page).getByRole("tab", { name, exact: true });

/** 从侧边栏打开页面，并等对应页签激活（页面真正渲染过才会登记页签）。 */
async function openFromSidebar(page: Page, section: string | null, item: string) {
  const sidebar = page.getByRole("complementary", { name: "Primary navigation" });
  const link = sidebar.getByRole("link", { name: item, exact: true });
  if (section && !(await link.isVisible())) await sidebar.getByRole("button", { name: section }).click();
  await link.click();
  await expect(tab(page, item)).toHaveAttribute("aria-selected", "true");
}

/** 整页跳转，并等页签栏登记完当前页面再继续。 */
async function gotoTab(page: Page, url: string, title: string) {
  await page.goto(url);
  await expect(tab(page, title)).toHaveAttribute("aria-selected", "true");
}

test.describe("workspace tabs", () => {
  test("sidebar navigation opens and activates tabs; the workbench tab is pinned", async ({ page }) => {
    await setup(page);
    await page.goto("/");

    await expect(tab(page, "Workbench")).toHaveAttribute("aria-selected", "true");
    await expect(tabBar(page).getByRole("button", { name: "Close Workbench" })).toHaveCount(0);

    await openFromSidebar(page, "Operator team", "Employees");
    await expect(page).toHaveURL(/\/users$/);
    await expect(tab(page, "Employees")).toHaveAttribute("aria-selected", "true");

    await openFromSidebar(page, "Operator team", "Roles");
    await expect(tab(page, "Roles")).toHaveAttribute("aria-selected", "true");
    await expect(tabBar(page).getByRole("tab")).toHaveCount(3);

    // 切回已有页签：不新增，只激活
    await tab(page, "Employees").click();
    await expect(page).toHaveURL(/\/users$/);
    await expect(tab(page, "Employees")).toHaveAttribute("aria-selected", "true");
    await expect(tabBar(page).getByRole("tab")).toHaveCount(3);

    // 固定页签的菜单里「关闭」不可用
    await tab(page, "Workbench").click({ button: "right" });
    await expect(page.getByRole("menuitem", { name: "Close", exact: true })).toHaveAttribute("data-disabled", "");
    await page.keyboard.press("Escape");
  });

  test("closing tabs: single, others, right and all", async ({ page }) => {
    await setup(page);
    await page.goto("/");
    await openFromSidebar(page, "Operator team", "Employees");
    await openFromSidebar(page, "Operator team", "Roles");
    // /settings 会 replace 到 /settings/profile，仍归在同一个「Settings」页签
    await openFromSidebar(page, null, "Settings");
    await expect(page).toHaveURL(/\/settings\/profile$/);
    await expect(tabBar(page).getByRole("tab")).toHaveText(["Workbench", "Employees", "Roles", "Settings"]);

    // 关闭当前页签：跳到相邻页签
    await tabBar(page).getByRole("button", { name: "Close Settings" }).click();
    await expect(tab(page, "Settings")).toHaveCount(0);
    await expect(page).toHaveURL(/\/roles$/);
    await expect(tab(page, "Roles")).toHaveAttribute("aria-selected", "true");

    // 右键 Employees → 关闭右侧：Roles 关掉，当前页回到 Employees
    await tab(page, "Employees").click({ button: "right" });
    await page.getByRole("menuitem", { name: "Close tabs to the right" }).click();
    await expect(tab(page, "Roles")).toHaveCount(0);
    await expect(page).toHaveURL(/\/users$/);

    // 关闭其他：保留固定的工作台和目标页签
    await openFromSidebar(page, "Operator team", "Roles");
    await tab(page, "Roles").click({ button: "right" });
    await page.getByRole("menuitem", { name: "Close others" }).click();
    await expect(tabBar(page).getByRole("tab")).toHaveText(["Workbench", "Roles"]);

    // 下拉菜单：关闭全部，只剩工作台
    await page.getByRole("button", { name: "Tab actions" }).click();
    await page.getByRole("menuitem", { name: "Close all" }).click();
    await expect(tabBar(page).getByRole("tab")).toHaveText(["Workbench"]);
    await expect(page).toHaveURL(/\/$/);
  });

  test("tabs survive a reload; query params stay in one tab and detail params split tabs", async ({ page }) => {
    await setup(page);
    await gotoTab(page, "/roles?page=2", "Roles");
    await gotoTab(page, "/users/u-bob", "Employees · u-bob");
    await gotoTab(page, "/users/u-dana", "Employees · u-dana");
    await gotoTab(page, "/users", "Employees");

    await page.reload();
    await expect(tabBar(page).getByRole("tab")).toHaveText([
      "Workbench",
      "Roles",
      "Employees · u-bob",
      "Employees · u-dana",
      "Employees",
    ]);
    await expect(tab(page, "Employees")).toHaveAttribute("aria-selected", "true");

    // 回到 Roles 页签时带上最后的查询参数
    await tab(page, "Roles").click();
    await expect(page).toHaveURL(/\/roles\?page=2$/);

    await tab(page, "Employees · u-bob").click();
    await expect(page).toHaveURL(/\/users\/u-bob$/);
  });

  test("tabs beyond the limit drop the oldest non-pinned tab", async ({ page }) => {
    await setup(page);
    for (let i = 1; i <= 10; i++) await gotoTab(page, `/users/u-${i}`, `Employees · u-${i}`);

    const tabs = tabBar(page).getByRole("tab");
    await expect(tabs).toHaveCount(10);
    await expect(tabs.first()).toHaveText("Workbench");
    await expect(tab(page, "Employees · u-1")).toHaveCount(0);
    await expect(tab(page, "Employees · u-10")).toHaveAttribute("aria-selected", "true");
  });

  test("redirect routes do not leave an intermediate tab", async ({ page }) => {
    await setup(page);
    await gotoTab(page, "/", "Workbench");
    await page.goto("/users/new");
    await expect(page).toHaveURL(/\/users$/);
    await expect(tabBar(page).getByRole("tab")).toHaveText(["Workbench", "Employees"]);
  });

  test("routes without permission never show up as tabs", async ({ page }) => {
    const perms = ADMIN_PERMS.filter((p) => p !== "Permissions.Tenants.View");
    await page.addInitScript((key) => {
      window.localStorage.setItem(key, JSON.stringify(["/", "/users", "/tenants"]));
    }, TABS_KEY);
    await setup(page, perms);

    await page.goto("/users");
    await expect(tab(page, "Employees")).toHaveAttribute("aria-selected", "true");
    await expect(tab(page, "Customer identity domains")).toHaveCount(0);

    await page.goto("/tenants");
    await expect(page.getByRole("main")).toContainText(/Access denied|Forbidden/i);
    await expect(tabBar(page).getByRole("tab")).toHaveText(["Workbench", "Employees"]);
  });

  test("signing out clears the stored tabs", async ({ page }) => {
    await setup(page);
    await page.goto("/users");
    await expect(tab(page, "Employees")).toBeVisible();
    await expect.poll(() => page.evaluate((key) => localStorage.getItem(key), TABS_KEY)).toContain("/users");

    await page.getByRole("button", { name: "Open profile menu", exact: true }).click();
    await page.getByRole("menuitem", { name: "Sign out", exact: true }).click();
    await page.getByRole("dialog", { name: "Sign out?" }).getByRole("button", { name: "Sign out", exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect(await page.evaluate((key) => localStorage.getItem(key), TABS_KEY)).toBeNull();
  });
});
