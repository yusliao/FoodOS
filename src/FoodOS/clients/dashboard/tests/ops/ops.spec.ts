import { expect, test, type Page } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { installShellMocks, paged } from "../helpers/shell-mocks";

const WAREHOUSE = {
  id: "33333333-3333-3333-3333-333333333333",
  code: "BOS-1",
  name: "Boston DC",
  city: "Boston",
  timeZoneId: "America/New_York",
  createdAtUtc: "2026-09-01T00:00:00Z",
  zones: [{ id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", code: "CH", kind: "Chilled" }],
};

const PO_LINE_ID = "88888888-8888-8888-8888-888888888888";
const PUTAWAY_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const LOCATION_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc";
const WAVE_ID = "dddddddd-dddd-dddd-dddd-dddddddddddd";
const PICK_ID = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
const LOT_ID = "ffffffff-ffff-ffff-ffff-ffffffffffff";
const SHIPMENT_ID = "12121212-1212-1212-1212-121212121212";
const ORDER_ID = "77777777-7777-7777-7777-777777777777";
const STOP_ID = "13131313-1313-1313-1313-131313131313";
const ORDER_LINE_ID = "14141414-1414-1414-1414-141414141414";

const OPS_PERMS = [
  "Permissions.Procurement.Purchase.View",
  "Permissions.Procurement.Quality.Pass",
  "Permissions.Procurement.Quality.Fail",
  "Permissions.Warehouse.Putaway.View",
  "Permissions.Warehouse.Putaway.Confirm",
  "Permissions.Warehouse.Locations.View",
  "Permissions.Warehouse.Waves.View",
  "Permissions.Warehouse.Waves.Generate",
  "Permissions.Warehouse.Waves.Release",
  "Permissions.Warehouse.Picks.View",
  "Permissions.Warehouse.Picks.Confirm",
  "Permissions.Logistics.Shipments.View",
  "Permissions.Logistics.Shipments.Load",
  "Permissions.Logistics.POD.Confirm",
];

async function mockOpsApis(page: Page) {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", OPS_PERMS);
  await mockJsonResponse(page, "**/api/v1/inventory/warehouses**", paged([WAREHOUSE]));
  await mockJsonResponse(page, "**/api/v1/procurement/purchase-orders**", [
    {
      id: "15151515-1515-1515-1515-151515151515",
      number: "PO202609130001",
      supplierId: "16161616-1616-1616-1616-161616161616",
      warehouseId: WAREHOUSE.id,
      status: "Receiving",
      expectedAt: new Date().toISOString(),
      createdAt: new Date().toISOString(),
      lines: [
        {
          id: PO_LINE_ID,
          productId: "44444444-4444-4444-4444-444444444444",
          zone: "Chilled",
          quantity: 8,
          receivedQty: 0,
          rejectedQty: 0,
        },
      ],
      qualityChecks: [],
    },
  ]);
  await mockJsonResponse(page, "**/api/v1/warehouse/putaway-tasks**", [
    {
      id: PUTAWAY_ID,
      warehouseId: WAREHOUSE.id,
      zoneId: WAREHOUSE.zones[0].id,
      zone: "Chilled",
      productId: "44444444-4444-4444-4444-444444444444",
      lotId: LOT_ID,
      quantity: 8,
      suggestedLocationId: LOCATION_ID,
      locationId: null,
      source: "QcPass",
      status: "Pending",
      createdAt: new Date().toISOString(),
    },
  ]);
  await mockJsonResponse(page, "**/api/v1/warehouse/locations**", [
    {
      id: LOCATION_ID,
      warehouseId: WAREHOUSE.id,
      zoneId: WAREHOUSE.zones[0].id,
      code: "C-ST-01",
      type: "Storage",
    },
  ]);
  await mockJsonResponse(page, "**/api/v1/warehouse/waves**", [
    {
      id: WAVE_ID,
      number: "W20260913-CH",
      dailyPlanId: "17171717-1717-1717-1717-171717171717",
      warehouseId: WAREHOUSE.id,
      zoneId: WAREHOUSE.zones[0].id,
      zone: "Chilled",
      businessDate: "2026-09-13",
      status: "Draft",
      createdAt: new Date().toISOString(),
      tasks: [],
    },
  ]);
  await mockJsonResponse(page, "**/api/v1/warehouse/pick-tasks/mine**", [
    {
      id: PICK_ID,
      waveId: WAVE_ID,
      orderId: ORDER_ID,
      orderLineId: ORDER_LINE_ID,
      productId: "44444444-4444-4444-4444-444444444444",
      locationId: LOCATION_ID,
      lotId: LOT_ID,
      lotNo: "LOT-A",
      quantity: 2,
      shortageQty: 0,
      status: "Pending",
    },
  ]);
  await mockJsonResponse(page, "**/api/v1/logistics/shipments**", [
    {
      id: SHIPMENT_ID,
      number: "SH202609130001",
      routeId: "18181818-1818-1818-1818-181818181818",
      warehouseId: WAREHOUSE.id,
      businessDate: "2026-09-13",
      vehicleId: "19191919-1919-1919-1919-191919191919",
      driverId: "20202020-2020-2020-2020-202020202020",
      status: "Created",
      createdAt: new Date().toISOString(),
      stops: [
        {
          id: STOP_ID,
          storeId: "11111111-1111-1111-1111-111111111111",
          sequence: 1,
          window: "05:00-08:00",
          status: "Pending",
          proofOfDelivery: null,
        },
      ],
      lines: [
        {
          id: "21212121-2121-2121-2121-212121212121",
          orderId: ORDER_ID,
          storeId: "11111111-1111-1111-1111-111111111111",
          toteId: null,
          lots: [
            {
              orderLineId: ORDER_LINE_ID,
              productId: "44444444-4444-4444-4444-444444444444",
              zone: "Chilled",
              lotId: LOT_ID,
              lotNo: "LOT-A",
              quantity: 2,
            },
          ],
        },
      ],
      returns: [],
    },
  ]);
}

test.beforeEach(async ({ page }) => {
  await seedAuthedSession(page, TEST_USER);
  await installShellMocks(page);
});

test("quality desk can pass an inbound line", async ({ page }) => {
  await mockOpsApis(page);
  let posted = false;
  await page.route("**/api/v1/procurement/purchase-orders/**/qc/pass", async (route) => {
    posted = true;
    await route.fulfill({ status: 200, headers: { "Content-Type": "application/json" }, body: JSON.stringify(LOT_ID) });
  });

  await page.goto("/ops/qc");
  await expect(page.getByRole("heading", { name: /quality desk/i })).toBeVisible();
  await page.getByLabel("Lot no").fill("LOT-QC");
  await page.getByTestId(`qc-pass-${PO_LINE_ID}`).click();
  await expect.poll(() => posted).toBe(true);
});

test("putaway confirms onto a storage location", async ({ page }) => {
  await mockOpsApis(page);
  let posted = false;
  await page.route("**/api/v1/warehouse/putaway-tasks/**/confirm", async (route) => {
    posted = true;
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        id: PUTAWAY_ID,
        warehouseId: WAREHOUSE.id,
        zoneId: WAREHOUSE.zones[0].id,
        zone: "Chilled",
        productId: "44444444-4444-4444-4444-444444444444",
        lotId: LOT_ID,
        quantity: 8,
        suggestedLocationId: LOCATION_ID,
        locationId: LOCATION_ID,
        source: "QcPass",
        status: "Completed",
        createdAt: new Date().toISOString(),
      }),
    });
  });

  await page.goto("/ops/putaway");
  await expect(page.getByRole("heading", { name: /putaway/i })).toBeVisible();
  await page.getByTestId(`putaway-confirm-${PUTAWAY_ID}`).click();
  await expect.poll(() => posted).toBe(true);
});

test("waves page generates a wave", async ({ page }) => {
  await mockOpsApis(page);
  let posted = false;
  await page.route("**/api/v1/warehouse/waves", async (route) => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    posted = true;
    await route.fulfill({ status: 200, headers: { "Content-Type": "application/json" }, body: "[]" });
  });

  await page.goto("/ops/waves");
  await expect(page.getByRole("heading", { name: /waves/i })).toBeVisible();
  await page.getByTestId("generate-waves").click();
  await expect.poll(() => posted).toBe(true);
});

test("pick page posts the scanned lot", async ({ page }) => {
  await mockOpsApis(page);
  let body: unknown;
  await page.route("**/api/v1/warehouse/pick-tasks/**/confirm", async (route) => {
    body = route.request().postDataJSON();
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        id: PICK_ID,
        waveId: WAVE_ID,
        orderId: ORDER_ID,
        orderLineId: ORDER_LINE_ID,
        productId: "44444444-4444-4444-4444-444444444444",
        locationId: LOCATION_ID,
        lotId: LOT_ID,
        lotNo: "LOT-A",
        quantity: 2,
        shortageQty: 0,
        status: "Packed",
      }),
    });
  });

  await page.goto("/ops/picks");
  await expect(page.getByRole("heading", { name: /pick tasks/i })).toBeVisible();
  await page.getByTestId(`pick-confirm-${PICK_ID}`).click();
  await expect.poll(() => (body as { scannedLotId?: string } | undefined)?.scannedLotId).toBe(LOT_ID);
});

test("load page scans an order onto the truck", async ({ page }) => {
  await mockOpsApis(page);
  let body: unknown;
  await page.route("**/api/v1/logistics/shipments/**/load", async (route) => {
    body = route.request().postDataJSON();
    await route.fulfill({
      status: 200,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        id: SHIPMENT_ID,
        number: "SH202609130001",
        routeId: "18181818-1818-1818-1818-181818181818",
        warehouseId: WAREHOUSE.id,
        businessDate: "2026-09-13",
        vehicleId: "19191919-1919-1919-1919-191919191919",
        driverId: "20202020-2020-2020-2020-202020202020",
        status: "Loading",
        createdAt: new Date().toISOString(),
        stops: [],
        lines: [],
        returns: [],
      }),
    });
  });

  await page.goto("/ops/shipments");
  await expect(page.getByRole("heading", { name: /load & delivery/i })).toBeVisible();
  await page.getByTestId(`load-scan-${SHIPMENT_ID}`).fill(ORDER_ID);
  await page.getByTestId(`load-confirm-${SHIPMENT_ID}`).click();
  await expect.poll(() => (body as { orderIds?: string[] } | undefined)?.orderIds?.[0]).toBe(ORDER_ID);
});
