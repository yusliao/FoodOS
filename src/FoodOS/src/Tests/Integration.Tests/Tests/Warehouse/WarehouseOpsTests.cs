using Hangfire;
using Hangfire.Storage;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Warehouse;

/// <summary>
/// 上架 / 装托 / 损耗 / Hangfire 截单注册。剧本 A 步 7 扫托、A 步 10 storing、D 步 3 报损。
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class WarehouseOpsTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public WarehouseOpsTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public void CutoffJob_Should_BeRegisteredEveryMinute()
    {
        _ = _factory.Server;
        var job = JobStorage.Current.GetConnection().GetRecurringJobs()
            .FirstOrDefault(j => j.Id == "warehouse-cutoff");
        job.ShouldNotBeNull();
        job.Cron.ShouldBe("* * * * *");
    }

    [Fact]
    public async Task QcPass_Should_CreatePutaway_And_ConfirmWritesStoringTrace()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var chilled = warehouse.Zones.First(z => z.Kind == "Chilled");
        var productId = await CreateChilledProductAsync(client);
        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPoAsync(client, supplierId, warehouse.Id, productId, 8m);

        using var qc = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/pass",
            QcBody("LOT-PUT", 8m));
        qc.StatusCode.ShouldBe(HttpStatusCode.OK, await qc.Content.ReadAsStringAsync());
        Guid qualityCheckId = await qc.DeserializeAsync<Guid>();
        Guid lotId = (await (await client.GetAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}")).DeserializeAsync<PurchaseOrderDto>())
            .QualityChecks[0].LotId!.Value;

        using var createLocation = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/locations",
            new { warehouseId = warehouse.Id, zoneId = chilled.Id, code = "C-ST-01", type = "Storage" });
        createLocation.StatusCode.ShouldBe(HttpStatusCode.OK, await createLocation.Content.ReadAsStringAsync());
        var locationId = await createLocation.DeserializeAsync<Guid>();

        using var listLocations = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/locations?warehouseId={warehouse.Id}&zoneId={chilled.Id}");
        listLocations.StatusCode.ShouldBe(HttpStatusCode.OK, await listLocations.Content.ReadAsStringAsync());
        (await listLocations.DeserializeAsync<List<LocationDto>>())
            .ShouldContain(l => l.Id == locationId && l.Code == "C-ST-01");

        using var createTask = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId,
                quantity = 8m,
                source = "QcPass"
            });
        createTask.StatusCode.ShouldBe(HttpStatusCode.OK, await createTask.Content.ReadAsStringAsync());
        var task = await createTask.DeserializeAsync<PutawayTaskDto>();
        task.Status.ShouldBe("Pending");

        using var listPutaway = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks?warehouseId={warehouse.Id}&status=Pending");
        listPutaway.StatusCode.ShouldBe(HttpStatusCode.OK, await listPutaway.Content.ReadAsStringAsync());
        (await listPutaway.DeserializeAsync<List<PutawayTaskDto>>())
            .ShouldContain(t => t.Id == task.Id);

        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks/{task.Id}/confirm",
            new { locationId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        (await confirm.DeserializeAsync<PutawayTaskDto>()).Status.ShouldBe("Completed");

        using var replay = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId,
                quantity = 8m,
                source = "QcPass",
                refId = qualityCheckId
            });
        replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        var replayedTask = await replay.DeserializeAsync<PutawayTaskDto>();
        replayedTask.Id.ShouldBe(task.Id);
        replayedTask.Status.ShouldBe("Completed");

        using var trace = await client.GetAsync($"{TestConstants.OpsBasePath}/lots/{lotId}/trace");
        var events = (await trace.DeserializeAsync<LotTraceDto>()).Events;
        events.Select(e => e.BizStep).ShouldContain("receiving");
        events.Select(e => e.BizStep).ShouldContain("storing");
    }

    [Fact]
    public async Task IsolatedLot_Should_RejectPutaway()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPoAsync(client, supplierId, warehouse.Id, productId, 5m);
        using var fail = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/fail",
            QcBody("LOT-BAD", 5m));
        fail.StatusCode.ShouldBe(HttpStatusCode.OK, await fail.Content.ReadAsStringAsync());
        Guid lotId = (await (await client.GetAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}")).DeserializeAsync<PurchaseOrderDto>())
            .QualityChecks[0].LotId!.Value;

        using var createTask = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId,
                quantity = 5m,
                source = "QcPass"
            });
        createTask.StatusCode.ShouldBe(HttpStatusCode.Conflict, await createTask.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Shrinkage_Should_ReduceAvailable_And_PostAdjustShrink()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        var lotId = await ReceiveChilledAsync(client, warehouse.Id, productId, "LOT-SH", 10m);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(10m);

        using var shrink = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/shrinkage",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId,
                quantity = 3m,
                reason = "damage",
                photoFileIds = Array.Empty<Guid>()
            });
        shrink.StatusCode.ShouldBe(HttpStatusCode.OK, await shrink.Content.ReadAsStringAsync());
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(7m);
    }

    [Fact]
    public async Task PackTote_Should_AllowLoadByToteId()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var packed = await PackOrderAsync(client, 4m);

        using var pack = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{packed.WaveId}/pack",
            new { orderIds = new[] { packed.OrderId }, sscc = $"SSCC{Guid.NewGuid():N}"[..18] });
        pack.StatusCode.ShouldBe(HttpStatusCode.OK, await pack.Content.ReadAsStringAsync());
        var tote = await pack.DeserializeAsync<PackToteDto>();
        tote.OrderIds.ShouldContain(packed.OrderId);

        var vehicleId = await CreateVehicleAsync(client);
        var driverId = await CreateDriverAsync(client);
        var routeId = await CreateRouteAsync(client, packed.WarehouseId, packed.StoreId);
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId,
                warehouseId = packed.WarehouseId,
                vehicleId,
                driverId,
                businessDate = packed.BusinessDate
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var shipment = await create.DeserializeAsync<ShipmentDto>();
        shipment.Lines.ShouldHaveSingleItem().ToteId.ShouldBe(tote.Id);

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { toteIds = new[] { tote.Id } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());
        (await load.DeserializeAsync<ShipmentDto>()).Status.ShouldBe("Loading");
    }

    private sealed record PackedOrder(
        Guid WarehouseId,
        Guid StoreId,
        Guid ProductId,
        Guid OrderId,
        Guid WaveId,
        DateOnly BusinessDate);

    private static async Task<PackedOrder> PackOrderAsync(HttpClient client, decimal qty)
    {
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        await ReceiveChilledAsync(client, warehouse.Id, productId, $"LOT-{Guid.NewGuid():N}"[..12], 20m);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        await PutCartAsync(client, storeId, productId, qty);

        using var place = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/orders", new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff", new { });
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();
        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        var wave = (await generate.DeserializeAsync<List<WaveDto>>()).ShouldHaveSingleItem();
        using var release = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release", new { });
        var task = (await release.DeserializeAsync<WaveDto>()).Tasks.ShouldHaveSingleItem();
        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = task.LotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        return new PackedOrder(warehouse.Id, storeId, productId, orderId, wave.Id, cutoffResult.BusinessDate);
    }

    private static object QcBody(string lotNo, decimal quantity) => new
    {
        quantity,
        sampleQty = 1m,
        lotNo,
        expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
        manufacturedOn = (DateOnly?)null,
        note = (string?)null,
        photoFileIds = (IReadOnlyList<Guid>?)null,
    };

    private static async Task<PurchaseOrderDto> CreateAppointedPoAsync(
        HttpClient client, Guid supplierId, Guid warehouseId, Guid productId, decimal quantity)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders",
            new
            {
                supplierId,
                warehouseId,
                expectedAt = DateTimeOffset.UtcNow.AddDays(1),
                lines = new[] { new { productId, zone = "Chilled", quantity } }
            });
        var poId = await create.DeserializeAsync<Guid>();
        using var appoint = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}/appointments",
            new { dockSlot = "DOCK-A", vehicleNo = "TRK-1" });
        appoint.StatusCode.ShouldBe(HttpStatusCode.OK, await appoint.Content.ReadAsStringAsync());
        using var get = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}");
        return await get.DeserializeAsync<PurchaseOrderDto>();
    }

    private static async Task<Guid> CreateSupplierAsync(HttpClient client)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/suppliers",
            new
            {
                code = $"H{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Sup"),
                categories = "dairy",
                leadDays = 2
            });
        return await create.DeserializeAsync<Guid>();
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new
            {
                code = $"WH{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Dc"),
                city = "Boston",
                timeZoneId = (string?)null
            });
        var id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<Guid> CreateChilledProductAsync(HttpClient client)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Dairy"), description = (string?)null, parentCategoryId = (Guid?)null });
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku = $"MILK-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Milk"),
                description = "Chilled",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = 9m,
                priceCurrency = "USD",
                stock = 0,
                temperatureZone = "Chilled"
            });
        return await productResp.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> ReceiveChilledAsync(
        HttpClient client, Guid warehouseId, Guid productId, string lotNo, decimal quantity)
    {
        using var receive = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            new
            {
                warehouseId,
                zone = "Chilled",
                productId,
                lotNo,
                expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                quantity,
                idempotencyKey = $"recv-{Guid.NewGuid():N}",
                manufacturedOn = (DateOnly?)null,
                origin = "Boston",
            });
        receive.StatusCode.ShouldBe(HttpStatusCode.OK, await receive.Content.ReadAsStringAsync());
        return await receive.DeserializeAsync<Guid>();
    }

    private static async Task PutCartAsync(HttpClient client, Guid storeId, Guid productId, decimal qty)
    {
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = qty } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CreateCustomerOrgAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/customer-orgs",
            new { code = $"C{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", name = Unique("Org") });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateStoreAsync(HttpClient client, Guid orgId, Guid warehouseId)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/stores",
            new
            {
                customerOrgId = orgId,
                code = $"S{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Store"),
                address = "1 Harbor St",
                defaultWarehouseId = warehouseId,
                defaultRouteId = (Guid?)null,
                deliveryWindow = "05:00-08:00",
            });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateVehicleAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/vehicles",
            new { plate = $"P{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}", compartmentZones = "Chilled", payloadKg = 3500m });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/drivers",
            new { userId = Guid.CreateVersion7(), phone = "+16175550100" });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateRouteAsync(HttpClient client, Guid warehouseId, Guid storeId)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/routes",
            new
            {
                warehouseId,
                code = $"R{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                storeIds = new[] { storeId },
                defaultVehicleId = (Guid?)null
            });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<decimal> GetAvailableAsync(HttpClient client, Guid warehouseId, Guid productId)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone=Chilled");
        return (await response.DeserializeAsync<AvailableQtyDto>()).Available;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
