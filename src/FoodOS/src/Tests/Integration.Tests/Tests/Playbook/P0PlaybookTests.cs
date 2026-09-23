using System.Globalization;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Playbook;

/// <summary>
/// P0 commercial preparation remains available while legacy local warehouse execution fails closed.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class P0PlaybookTests
{
    private readonly AuthHelper _auth;

    public P0PlaybookTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ScenarioA_Should_KeepCommercialPreparation_AndBlockLocalWmsExecution()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client, listPrice: 20m);

        var orgA = await CreateCustomerOrgAsync(client);
        var orgB = await CreateCustomerOrgAsync(client);
        await CreatePriceListAsync(client, orgA, productId, 8m);
        await CreatePriceListAsync(client, orgB, productId, 12m);

        var quoteA = await QuoteAsync(client, orgA, productId, 6m);
        quoteA.UnitPrice.ShouldBe(8m);
        quoteA.Source.ShouldBe("Contract");
        (await QuoteAsync(client, orgB, productId, 6m)).UnitPrice.ShouldBe(12m);
        using var listA = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/price-lists?customerOrgId={orgA}");
        listA.StatusCode.ShouldBe(HttpStatusCode.OK, await listA.Content.ReadAsStringAsync());
        (await listA.DeserializeAsync<List<PriceListDto>>()).SelectMany(list => list.Lines)
            .ShouldNotContain(line => line.UnitPrice == 12m);

        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPurchaseOrderAsync(client, supplierId, warehouse.Id, productId, 20m);
        po.Status.ShouldBe("Receiving");
        using var qc = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/pass",
            QcBody("LOT-MILK-NEAR", 20m));
        await AssertBlockedAsync(qc);
        using var getPo = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var unchangedPo = await getPo.DeserializeAsync<PurchaseOrderDto>();
        unchangedPo.Status.ShouldBe("Receiving");
        unchangedPo.QualityChecks.ShouldBeEmpty();

        var chilled = warehouse.Zones.First(zone => zone.Kind == "Chilled");
        using var createLocation = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/locations",
            new { warehouseId = warehouse.Id, zoneId = chilled.Id, code = "C-ST-A", type = "Storage" });
        await AssertBlockedAsync(createLocation);
        using var createTask = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Chilled",
                productId,
                lotId = Guid.NewGuid(),
                quantity = 20m,
                source = "QcPass",
            });
        await AssertBlockedAsync(createTask);

        var storeId = await CreateStoreAsync(client, orgA, warehouse.Id);
        await PutCartAsync(client, storeId, productId, 6m);
        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId });
        await AssertBlockedAsync(place);
        await AssertCartAsync(client, storeId, productId, 6m);

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff", new { });
        await AssertBlockedAsync(cutoff);
        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await AssertBlockedAsync(generate);
        using var createShipment = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId = Guid.NewGuid(),
                warehouseId = warehouse.Id,
                vehicleId = Guid.NewGuid(),
                driverId = Guid.NewGuid(),
                businessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        await AssertBlockedAsync(createShipment);

        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);
        using var orders = await client.GetAsync(
            $"{TestConstants.OrderingBasePath}/orders?storeId={storeId}&pageNumber=1&pageSize=20");
        orders.StatusCode.ShouldBe(HttpStatusCode.OK, await orders.Content.ReadAsStringAsync());
        (await orders.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScenarioC_Should_BlockBothPlacements_WithoutLocalAtpReservation()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client, listPrice: 9m);
        var orgA = await CreateCustomerOrgAsync(client);
        var orgB = await CreateCustomerOrgAsync(client);
        var storeA = await CreateStoreAsync(client, orgA, warehouse.Id);
        var storeB = await CreateStoreAsync(client, orgB, warehouse.Id);
        await PutCartAsync(client, storeA, productId, 8m);
        await PutCartAsync(client, storeB, productId, 8m);

        using var placeA = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId = storeA });
        await AssertBlockedAsync(placeA);
        using var placeB = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId = storeB });
        await AssertBlockedAsync(placeB);

        await AssertCartAsync(client, storeA, productId, 8m);
        await AssertCartAsync(client, storeB, productId, 8m);
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(0m);
        using var orders = await client.GetAsync(
            $"{TestConstants.OrderingBasePath}/orders?pageNumber=1&pageSize=200");
        orders.StatusCode.ShouldBe(HttpStatusCode.OK, await orders.Content.ReadAsStringAsync());
        var items = (await orders.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items;
        items.ShouldNotContain(order => order.StoreId == storeA || order.StoreId == storeB);
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

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private static async Task AssertCartAsync(
        HttpClient client, Guid storeId, Guid productId, decimal quantity)
    {
        using var response = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var line = (await response.DeserializeAsync<CartDto>()).Lines.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(productId);
        line.Quantity.ShouldBe(quantity);
    }

    private static async Task<PurchaseOrderDto> CreateAppointedPurchaseOrderAsync(
        HttpClient client,
        Guid supplierId,
        Guid warehouseId,
        Guid productId,
        decimal quantity)
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
                name = Unique("Harbor"),
                categories = "dairy",
                leadDays = 2
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        return await create.DeserializeAsync<Guid>();
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        var code = $"BOS{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<Guid> CreateChilledProductAsync(HttpClient client, decimal listPrice)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brandResp.StatusCode.ShouldBe(HttpStatusCode.OK, await brandResp.Content.ReadAsStringAsync());

        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Dairy"), description = (string?)null, parentCategoryId = (Guid?)null });
        categoryResp.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResp.Content.ReadAsStringAsync());

        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku = $"MILK-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Milk"),
                description = "Chilled SKU",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = listPrice,
                priceCurrency = "USD",
                stock = 0,
                temperatureZone = "Chilled",
                shelfLifeDays = 14,
                minRemainingDaysOnShip = 3,
            });
        productResp.StatusCode.ShouldBe(HttpStatusCode.OK, await productResp.Content.ReadAsStringAsync());
        return await productResp.DeserializeAsync<Guid>();
    }

    private static async Task CreatePriceListAsync(
        HttpClient client,
        Guid customerOrgId,
        Guid productId,
        decimal unitPrice)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/price-lists",
            new
            {
                name = Unique("List"),
                customerOrgId,
                validFrom = DateTimeOffset.UtcNow.AddDays(-1),
                validTo = DateTimeOffset.UtcNow.AddYears(1),
                priority = 1,
                lines = new[] { new { productId, minQty = 1m, unitPrice, currency = "USD" } }
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PriceQuoteDto> QuoteAsync(
        HttpClient client,
        Guid customerOrgId,
        Guid productId,
        decimal quantity)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/quotes?customerOrgId={customerOrgId}&productId={productId}&quantity={quantity.ToString(CultureInfo.InvariantCulture)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<PriceQuoteDto>();
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
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
