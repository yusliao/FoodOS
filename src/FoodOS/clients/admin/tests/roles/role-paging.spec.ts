import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks, paged } from "../helpers/shell-mocks";

async function setup(page: Page, permissions = ["Permissions.Roles.View", "Permissions.Users.View"]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  const queries: URL[] = [];
  await page.route("**/identity/roles?*", async route => {
    expect(route.request().headers().tenant).toBe("root");
    const url = new URL(route.request().url());
    queries.push(url);
    const pageNumber = Number(url.searchParams.get("PageNumber"));
    const pageSize = Number(url.searchParams.get("PageSize"));
    const search = url.searchParams.get("Search");
    const name = search ? "Remote searched role" : pageNumber === 1 ? "First role" : "Later role";
    await route.fulfill({ json: paged([{ id: name, name }], { pageNumber, pageSize, totalCount: search ? 1 : pageSize + 1 }) });
  });
  await page.route("**/identity/users/search?*", route => route.fulfill({ json: paged([]) }));
  return queries;
}

test("directory reaches later pages and server search resets to page one", async ({ page }) => {
  const queries = await setup(page);
  await page.goto("/roles");
  await expect(page.getByRole("button", { name: /^First role/ })).toBeVisible();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Later role/ })).toBeVisible();
  await page.getByRole("searchbox").fill("remote");
  await expect(page.getByRole("button", { name: /^Remote searched role/ })).toBeVisible();
  expect(queries.at(-1)?.searchParams.get("Search")).toBe("remote");
  expect(queries.at(-1)?.searchParams.get("PageNumber")).toBe("1");
  await expect(page.getByRole("button", { name: "Previous", exact: true })).toBeDisabled();
});

test("directory does not show previous page rows while the next page loads", async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/identity/roles?*", async route => {
    if (new URL(route.request().url()).searchParams.get("PageNumber") !== "2") return route.fallback();
    await gate;
    await route.fallback();
  });
  await page.goto("/roles");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  try {
    await expect(page.getByRole("button", { name: /^First role/ })).toHaveCount(0);
    await expect(page.getByText("Loading roles", { exact: true })).toBeVisible();
  } finally { release(); }
  await expect(page.getByRole("button", { name: /^Later role/ })).toBeVisible();
});

test("directory later page error retries the same page", async ({ page }) => {
  await setup(page);
  let fail = true;
  await page.route("**/identity/roles?*", async route => {
    if (new URL(route.request().url()).searchParams.get("PageNumber") !== "2" || !fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Role page unavailable" } });
  });
  await page.goto("/roles");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(page.getByText("Role page unavailable")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Later role/ })).toBeVisible();
});

test("employee role filter loads on demand, pages and preserves selected role during search", async ({ page }) => {
  const queries = await setup(page);
  await page.goto("/users");
  const filter = page.getByRole("region", { name: "Role", exact: true });
  await expect(filter).toBeVisible();
  expect(queries).toHaveLength(0);
  await filter.getByRole("button", { name: /Any role/ }).click();
  await filter.getByRole("button", { name: "Next", exact: true }).click();
  const request = page.waitForRequest(req => req.url().includes("/users/search?") && new URL(req.url()).searchParams.get("RoleId") === "Later role");
  await filter.getByRole("button", { name: "Later role", exact: true }).click();
  expect((await request).headers().tenant).toBe("root");
  await filter.getByRole("textbox").fill("remote");
  await expect(filter.getByRole("button", { name: "Remote searched role", exact: true })).toBeVisible();
  await expect(filter.getByRole("button", { name: "Role: Later role", exact: true })).toBeVisible();
  expect(queries.at(-1)?.searchParams.get("PageNumber")).toBe("1");
  await filter.getByRole("button", { name: "Any role", exact: true }).click();
  await expect(filter.getByRole("button", { name: "Role: Any role", exact: true })).toBeVisible();
});

test("employee viewer without role view never requests the role catalog or sees creation", async ({ page }) => {
  const queries = await setup(page, ["Permissions.Users.View"]);
  await page.goto("/users");
  await expect(page.getByRole("heading", { name: "Directory", exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Role", exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "New user", exact: true })).toHaveCount(0);
  expect(queries).toHaveLength(0);
});

test("employee role filter failure retries and Chinese mobile layout fits", async ({ page }) => {
  await setup(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  let fail = true;
  await page.route("**/identity/roles?*", async route => {
    if (!fail) return route.fallback();
    await route.fulfill({ status: 403, json: { detail: "Cannot load roles" } });
  });
  await page.goto("/users");
  const filter = page.getByRole("region");
  await filter.getByRole("button").click();
  await expect(page.getByText("Cannot load roles")).toBeVisible();
  fail = false;
  await page.getByRole("button", { name: "重试", exact: true }).click();
  await expect(filter.getByRole("button", { name: "First role", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
