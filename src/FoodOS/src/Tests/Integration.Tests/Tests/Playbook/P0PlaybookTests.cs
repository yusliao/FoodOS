using System.Globalization;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Playbook;

/// <summary>
/// P0 剧本 A–E 用与 seed-demo 同形态的主数据走 HTTP 闭环。
/// A 步 7 装车扫 OrderId（PackTote 未做）。A 步 10 无 storing（上架未做）。
/// D 步 3 返仓上架/报损未做。B / C.2–C.3 / D.2 的切片测试仍有效。
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
    public async Task ScenarioA_Should_CloseLoop_WithContractPrice_AndLotTrace()
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
        var quoteB = await QuoteAsync(client, orgB, productId, 6m);
        quoteB.UnitPrice.ShouldBe(12m);

        using var listA = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/price-lists?customerOrgId={orgA}");
        var listsA = await listA.DeserializeAsync<List<PriceListDto>>();
        listsA.SelectMany(l => l.Lines).ShouldNotContain(l => l.UnitPrice == 12m);

        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPurchaseOrderAsync(client, supplierId, warehouse.Id, productId, 20m);
        po.Status.ShouldBe("Receiving");
        var lineId = po.Lines.ShouldHaveSingleItem().Id;

        using var qc = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{lineId}/qc/pass",
            QcBody("LOT-MILK-NEAR", 20m));
        qc.StatusCode.ShouldBe(HttpStatusCode.OK, await qc.Content.ReadAsStringAsync());

        using var getPo = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var receivedPo = await getPo.DeserializeAsync<PurchaseOrderDto>();
        var qcCheck = receivedPo.QualityChecks.ShouldHaveSingleItem();
        qcCheck.LotId.HasValue.ShouldBeTrue();
        Guid lotId = qcCheck.LotId!.Value;

        var availableAfterQc = await GetAvailableAsync(client, warehouse.Id, productId);
        availableAfterQc.ShouldBe(20m);

        var storeId = await CreateStoreAsync(client, orgA, warehouse.Id);
        await PutCartAsync(client, storeId, productId, 6m);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var getPlaced = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var placed = await getPlaced.DeserializeAsync<SalesOrderDto>();
        placed.Status.ShouldBe("Reserved");
        placed.Lines.ShouldHaveSingleItem().UnitPrice.ShouldBe(8m);
        placed.Lines[0].OrderedQty.ShouldBe(6m);
        placed.Lines[0].ReservationId.ShouldNotBeNull();
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(14m);

        using var amend = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId, quantity = 4m } } });
        amend.StatusCode.ShouldBe(HttpStatusCode.OK, await amend.Content.ReadAsStringAsync());
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(16m);

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff",
            new { });
        cutoff.StatusCode.ShouldBe(HttpStatusCode.OK, await cutoff.Content.ReadAsStringAsync());
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();
        cutoffResult.OrdersLocked.ShouldBe(1);

        using var getLocked = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await getLocked.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Planned");

        using var amendAfterCutoff = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId, quantity = 2m } } });
        amendAfterCutoff.StatusCode.ShouldBe(HttpStatusCode.Conflict, await amendAfterCutoff.Content.ReadAsStringAsync());

        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        generate.StatusCode.ShouldBe(HttpStatusCode.OK, await generate.Content.ReadAsStringAsync());
        var wave = (await generate.DeserializeAsync<List<WaveDto>>()).ShouldHaveSingleItem();

        using var release = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release",
            new { });
        release.StatusCode.ShouldBe(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        var released = await release.DeserializeAsync<WaveDto>();
        var task = released.Tasks.ShouldHaveSingleItem();
        task.LotId.ShouldBe(lotId);

        using var wrong = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = Guid.CreateVersion7() });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await wrong.Content.ReadAsStringAsync());
        using var stillPicking = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await stillPicking.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Picking");

        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = lotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        using var packed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await packed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Packed");

        var vehicleId = await CreateVehicleAsync(client);
        var driverId = await CreateDriverAsync(client);
        var routeId = await CreateRouteAsync(client, warehouse.Id, storeId);

        using var createShipment = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId,
                warehouseId = warehouse.Id,
                vehicleId,
                driverId,
                businessDate = cutoffResult.BusinessDate
            });
        createShipment.StatusCode.ShouldBe(HttpStatusCode.OK, await createShipment.Content.ReadAsStringAsync());
        var shipment = await createShipment.DeserializeAsync<ShipmentDto>();
        var shipmentLine = shipment.Lines.ShouldHaveSingleItem();
        shipmentLine.OrderId.ShouldBe(orderId);

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { orderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());

        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());
        using var inTransit = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await inTransit.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("InTransit");

        var stop = shipment.Stops.ShouldHaveSingleItem();
        var shipLot = shipmentLine.Lots.ShouldHaveSingleItem();
        using var pod = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{stop.Id}/pod",
            new
            {
                lines = new[]
                {
                    new { orderLineId = shipLot.OrderLineId, lotId, signedQty = 4m }
                },
                signerName = "Chef Lee",
                photoFileIds = Array.Empty<Guid>(),
                geo = "42.36,-71.06"
            });
        pod.StatusCode.ShouldBe(HttpStatusCode.OK, await pod.Content.ReadAsStringAsync());

        using var received = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var receivedOrder = await received.DeserializeAsync<SalesOrderDto>();
        receivedOrder.Status.ShouldBe("Received");
        receivedOrder.Lines[0].Lots.ShouldHaveSingleItem().LotId.ShouldBe(lotId);

        using var reconcile = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/reconcile",
            new { });
        reconcile.StatusCode.ShouldBe(HttpStatusCode.OK, await reconcile.Content.ReadAsStringAsync());
        using var closed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await closed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Reconciled");

        using var traceResponse = await client.GetAsync($"{TestConstants.OpsBasePath}/lots/{lotId}/trace");
        traceResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await traceResponse.Content.ReadAsStringAsync());
        var trace = await traceResponse.DeserializeAsync<LotTraceDto>();
        trace.LotId.ShouldBe(lotId);
        trace.Events.Select(e => e.BizStep).ToArray().ShouldBe(["receiving", "picking", "shipping", "arriving"]);
        trace.Events.ShouldNotContain(e => e.BizStep == "storing");
        trace.Events.Select(e => e.OccurredAt).ShouldBe(trace.Events.Select(e => e.OccurredAt).OrderBy(t => t));
        trace.Events[0].Module.ShouldBe("Procurement");
        trace.Events[1].Module.ShouldBe("Warehouse");
        trace.Events[2].Module.ShouldBe("Logistics");
        trace.Events[3].Module.ShouldBe("Logistics");
    }

    [Fact]
    public async Task ScenarioC_Should_RejectSecondPlace_When_AtpWouldOversell()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateChilledProductAsync(client, listPrice: 9m);
        var supplierId = await CreateSupplierAsync(client);
        var po = await CreateAppointedPurchaseOrderAsync(client, supplierId, warehouse.Id, productId, 10m);
        using var qc = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}/lines/{po.Lines[0].Id}/qc/pass",
            QcBody("LOT-ATP", 10m));
        qc.StatusCode.ShouldBe(HttpStatusCode.OK, await qc.Content.ReadAsStringAsync());

        var orgA = await CreateCustomerOrgAsync(client);
        var orgB = await CreateCustomerOrgAsync(client);
        var storeA = await CreateStoreAsync(client, orgA, warehouse.Id);
        var storeB = await CreateStoreAsync(client, orgB, warehouse.Id);
        await PutCartAsync(client, storeA, productId, 8m);
        await PutCartAsync(client, storeB, productId, 8m);

        using var placeA = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId = storeA });
        placeA.StatusCode.ShouldBe(HttpStatusCode.OK, await placeA.Content.ReadAsStringAsync());

        using var placeB = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId = storeB });
        placeB.StatusCode.ShouldBe(HttpStatusCode.Conflict, await placeB.Content.ReadAsStringAsync());
        (await GetAvailableAsync(client, warehouse.Id, productId)).ShouldBe(2m);
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

    private static async Task<Guid> CreateVehicleAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/vehicles",
            new
            {
                plate = $"P{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}",
                compartmentZones = "Ambient,Chilled",
                payloadKg = 3500m
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/drivers",
            new { userId = Guid.CreateVersion7(), phone = "+16175550100" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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
