using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Hangfire;
using Hangfire.Storage;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Warehouse;

/// <summary>
/// External-WMS mode retires local QC, putaway, shrinkage, packing and shipment execution while keeping their reads safe.
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
    public void LegacyCutoffJob_Should_NotBeScheduled_InExternalWmsMode()
    {
        _ = _factory.Server;
        using var connection = JobStorage.Current.GetConnection();
        connection.GetRecurringJobs().ShouldNotContain(job => job.Id == "warehouse-cutoff");
    }

    [Fact]
    public async Task QcPass_Should_BeBlocked_WithoutCreatingPutawayOrTrace()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPoAsync(client, supplierId, warehouse.Id, productId, 8m);

        using var qc = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/pass",
            QcBody("LOT-PUT", 8m));
        await AssertBlockedAsync(qc);

        using var createTask = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId = Guid.NewGuid(),
                quantity = 8m,
                source = "QcPass"
            });
        await AssertBlockedAsync(createTask);

        using var listPutaway = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks?warehouseId={warehouse.Id}");
        listPutaway.StatusCode.ShouldBe(HttpStatusCode.OK, await listPutaway.Content.ReadAsStringAsync());
        (await listPutaway.DeserializeAsync<List<PutawayTaskDto>>()).ShouldBeEmpty();
        using var getPo = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var unchanged = await getPo.DeserializeAsync<PurchaseOrderDto>();
        unchanged.QualityChecks.ShouldBeEmpty();
        unchanged.Lines[0].ReceivedQty.ShouldBe(0m);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);
    }

    [Fact]
    public async Task QcFail_Should_BeBlocked_WithoutCreatingIsolatedLotOrPutaway()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPoAsync(client, supplierId, warehouse.Id, productId, 5m);

        using var fail = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/fail",
            QcBody("LOT-BAD", 5m));
        await AssertBlockedAsync(fail);
        using var createTask = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId = Guid.NewGuid(),
                quantity = 5m,
                source = "QcPass"
            });
        await AssertBlockedAsync(createTask);

        using var getPo = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var unchanged = await getPo.DeserializeAsync<PurchaseOrderDto>();
        unchanged.QualityChecks.ShouldBeEmpty();
        unchanged.Lines[0].RejectedQty.ShouldBe(0m);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);
    }

    [Fact]
    public async Task Shrinkage_Should_BeBlockedWithoutChangingAvailable()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);

        using var shrink = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/shrinkage",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId = Guid.NewGuid(),
                quantity = 3m,
                reason = "damage",
                photoFileIds = Array.Empty<Guid>()
            });
        await AssertBlockedAsync(shrink);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);
    }

    [Fact]
    public async Task PackAndShipmentExecution_Should_BeBlockedBeforeCreatingLocalRecords()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);

        using var pack = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{Guid.NewGuid()}/pack",
            new { orderIds = new[] { Guid.NewGuid() }, sscc = $"SSCC{Guid.NewGuid():N}"[..18] });
        await AssertBlockedAsync(pack);
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId = Guid.NewGuid(),
                warehouseId = warehouse.Id,
                vehicleId = Guid.NewGuid(),
                driverId = Guid.NewGuid(),
                businessDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        await AssertBlockedAsync(create);
        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{Guid.NewGuid()}/load",
            new { toteIds = new[] { Guid.NewGuid() } });
        await AssertBlockedAsync(load);
        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{Guid.NewGuid()}/depart", new { });
        await AssertBlockedAsync(depart);
    }

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
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
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
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
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
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
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<Guid> CreateChilledProductAsync(HttpClient client)
    {
        using var brand = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brand.StatusCode.ShouldBe(HttpStatusCode.OK, await brand.Content.ReadAsStringAsync());
        using var category = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Dairy"), description = (string?)null, parentCategoryId = (Guid?)null });
        category.StatusCode.ShouldBe(HttpStatusCode.OK, await category.Content.ReadAsStringAsync());
        using var product = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku = $"MILK-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Milk"),
                description = "Chilled",
                brandId = await brand.DeserializeAsync<Guid>(),
                categoryId = await category.DeserializeAsync<Guid>(),
                priceAmount = 9m,
                priceCurrency = "USD",
                stock = 0,
                temperatureZone = "Chilled"
            });
        product.StatusCode.ShouldBe(HttpStatusCode.OK, await product.Content.ReadAsStringAsync());
        return await product.DeserializeAsync<Guid>();
    }

    private static async Task<decimal> GetAvailableAsync(HttpClient client, Guid warehouseId, Guid productId)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone=Chilled");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.DeserializeAsync<AvailableQtyDto>()).Available;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
