import { expect, test, type Page } from "@playwright/test";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installAdminShellMocks } from "../helpers/shell-mocks";
const view = "Permissions.Logistics.Vehicles.View";
const create = "Permissions.Logistics.Vehicles.Create";
const vehicle = { id: "v1", plate: "TRUCK-1", compartmentZones: "Ambient,Chilled", payloadKg: 1000 };
async function setup(page: Page, permissions: string[]) {
  await seedAuthedSession(page, { ...TEST_USER, permissions });
  await installAdminShellMocks(page, permissions);
  await page.route("**/api/v1/logistics/vehicles", route => route.fulfill({ json: [vehicle] }));
}
test("read-only navigation requests only vehicles with root identity", async ({ page }) => {
  await setup(page, [view]);
  const requests: string[] = [];
  page.on("request", request => { if (request.url().includes("/api/v1/logistics")) { requests.push(request.url()); expect(request.headers().tenant).toBe("root"); } });
  await page.goto("/");
  await page.getByRole("main").getByRole("link", { name: "Vehicles", exact: true }).click();
  await expect(page.getByRole("heading", { name: "TRUCK-1" })).toBeVisible();
  await expect(page.getByRole("button", { name: "New vehicle" })).toHaveCount(0);
  expect(requests.every(url => url.endsWith("/logistics/vehicles"))).toBe(true);
});
for (const permissions of [[], [create]]) test(`direct URL requires view: ${permissions.length}`, async ({ page }) => {
  await setup(page, permissions);
  let requests = 0;
  page.on("request", request => { if (request.url().includes("/api/v1/logistics")) requests++; });
  await page.goto("/logistics/vehicles");
  await expect(page.getByRole("heading", { name: "You don't hold the permissions to view this surface." })).toBeVisible();
  expect(requests).toBe(0);
});
test("403 retries to empty without disguising a failed list", async ({ page }) => {
  await setup(page, [view]);
  let failed = true;
  await page.route("**/api/v1/logistics/vehicles", route => route.fulfill(failed ? { status: 403, json: { detail: "Vehicle access denied" } } : { json: [] }));
  await page.goto("/logistics/vehicles");
  await expect(page.getByText("Vehicle access denied")).toBeVisible();
  await expect(page.getByText("No vehicles registered.")).toHaveCount(0);
  failed = false;
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByText("No vehicles registered.")).toBeVisible();
});
test("loading does not appear as an empty vehicle list", async ({ page }) => {
  await setup(page, [view]);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/logistics/vehicles", async route => { await pending; await route.fulfill({ json: [] }); });
  await page.goto("/logistics/vehicles");
  await expect(page.getByText("Loading vehicles…")).toBeVisible();
  await expect(page.getByText("No vehicles registered.")).toHaveCount(0);
  release();
  await expect(page.getByText("No vehicles registered.")).toBeVisible();
});
test("creation validates capacity and preserves values and retry key", async ({ page }) => {
  await setup(page, [view, create]);
  const writes: { key: string; body: unknown }[] = [];
  await page.route("**/api/v1/logistics/vehicles", route => {
    if (route.request().method() === "GET") return route.fulfill({ json: writes.length >= 3 ? [vehicle] : [] });
    expect(route.request().headers().tenant).toBe("root");
    writes.push({ key: route.request().headers()["idempotency-key"], body: route.request().postDataJSON() });
    return route.fulfill(writes.length < 3 ? { status: 500, json: { detail: "Save failed" } } : { json: "v1" });
  });
  await page.goto("/logistics/vehicles");
  await page.getByRole("button", { name: "New vehicle" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("License plate").fill("TRUCK-1");
  await dialog.getByLabel("Compartment zones").fill("Ambient,Chilled");
  await dialog.getByLabel("Payload (kg)").fill("0");
  await expect(dialog.getByRole("button", { name: "Register vehicle" })).toBeDisabled();
  await dialog.getByLabel("Payload (kg)").fill("1000");
  await dialog.getByRole("button", { name: "Register vehicle" }).click();
  await expect(dialog.getByText("Save failed")).toBeVisible();
  await expect(dialog.getByLabel("License plate")).toHaveValue("TRUCK-1");
  await dialog.getByRole("button", { name: "Register vehicle" }).click();
  await expect.poll(() => writes.length).toBe(2);
  await dialog.getByLabel("Payload (kg)").fill("1200");
  await dialog.getByRole("button", { name: "Register vehicle" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "TRUCK-1" })).toBeVisible();
  expect(writes[0].key).toBeTruthy();
  expect(writes[0]).toEqual(writes[1]);
  expect(writes[2].key).not.toBe(writes[0].key);
  expect(writes[2].body).toEqual({ plate: "TRUCK-1", compartmentZones: "Ambient,Chilled", payloadKg: 1200 });
});
test("Chinese mobile vehicle form and cards fit viewport", async ({ page }) => {
  await setup(page, [view, create]);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.addInitScript(() => localStorage.setItem("foodos.culture", "zh-CN"));
  await page.goto("/logistics/vehicles");
  await expect(page.getByRole("heading", { name: "车辆档案" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "TRUCK-1" })).toBeVisible();
  await page.getByRole("button", { name: "新增车辆" }).click();
  await expect(page.getByRole("dialog").getByLabel("车牌号")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});
