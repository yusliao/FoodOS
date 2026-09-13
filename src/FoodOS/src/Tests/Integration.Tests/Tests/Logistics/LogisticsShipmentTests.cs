using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Logistics;

/// <summary>
/// 剧本 A 步 7–9 + 剧本 D 步 2–3：装车发运、电子签收、对账、随车退、返仓待上架、报损入看板。
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class LogisticsShipmentTests
{
    private readonly AuthHelper _auth;

    public LogisticsShipmentTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task DepartAndFullPod_Should_MoveOrderToReconciled_WithLotOnOrder()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var packed = await PackOrderAsync(client, orderQty: 6m);

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
        shipment.Status.ShouldBe("Created");

        using var listShipments = await client.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments?warehouseId={packed.WarehouseId}");
        listShipments.StatusCode.ShouldBe(HttpStatusCode.OK, await listShipments.Content.ReadAsStringAsync());
        (await listShipments.DeserializeAsync<List<ShipmentDto>>())
            .ShouldContain(s => s.Id == shipment.Id);

        var line = shipment.Lines.ShouldHaveSingleItem();
        line.OrderId.ShouldBe(packed.OrderId);
        var lot = line.Lots.ShouldHaveSingleItem();
        lot.LotId.ShouldBe(packed.LotId);

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { packed.OrderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());
        (await load.DeserializeAsync<ShipmentDto>()).Status.ShouldBe("Loading");

        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());
        (await depart.DeserializeAsync<ShipmentDto>()).Status.ShouldBe("Departed");

        using var inTransit = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await inTransit.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("InTransit");

        var stop = shipment.Stops.ShouldHaveSingleItem();
        using var pod = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{stop.Id}/pod",
            new
            {
                lines = new[]
                {
                    new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = packed.Quantity }
                },
                signerName = "Chef Lee",
                photoFileIds = Array.Empty<Guid>(),
                geo = "42.36,-71.06"
            });
        pod.StatusCode.ShouldBe(HttpStatusCode.OK, await pod.Content.ReadAsStringAsync());
        var signed = await pod.DeserializeAsync<ShipmentDto>();
        signed.Status.ShouldBe("Completed");
        signed.Returns.ShouldBeEmpty();

        using var received = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        var receivedOrder = await received.DeserializeAsync<SalesOrderDto>();
        receivedOrder.Status.ShouldBe("Received");
        receivedOrder.Lines[0].DeliveredQty.ShouldBe(packed.Quantity);
        receivedOrder.Lines[0].Lots.ShouldHaveSingleItem().LotId.ShouldBe(packed.LotId);

        using var reconcile = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}/reconcile",
            new { });
        reconcile.StatusCode.ShouldBe(HttpStatusCode.OK, await reconcile.Content.ReadAsStringAsync());
        using var closed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await closed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Reconciled");

        using var claim = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/after-sales",
            new
            {
                orderId = packed.OrderId,
                orderLineId = receivedOrder.Lines[0].Id,
                type = "Return",
                quantity = 1m,
                reason = "bruised",
            });
        claim.StatusCode.ShouldBe(HttpStatusCode.OK, await claim.Content.ReadAsStringAsync());
        var ticket = await claim.DeserializeAsync<AfterSalesTicketDto>();
        ticket.Type.ShouldBe("Return");
        ticket.Status.ShouldBe("Applied");
        ticket.Quantity.ShouldBe(1m);

        using var listed = await client.GetAsync(
            $"{TestConstants.OrderingBasePath}/after-sales?storeId={packed.StoreId}");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        (await listed.DeserializeAsync<List<AfterSalesTicketDto>>())
            .ShouldContain(t => t.Id == ticket.Id);

        using var afterClaim = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await afterClaim.DeserializeAsync<SalesOrderDto>()).Lines[0].ReturnedQty.ShouldBe(1m);
    }

    [Fact]
    public async Task PartialReject_Should_WriteReturnOnTruck_And_RestoreOnHand()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var packed = await PackOrderAsync(client, orderQty: 5m);
        decimal availableBefore = await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId);

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
        var lot = shipment.Lines.ShouldHaveSingleItem().Lots.ShouldHaveSingleItem();

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { packed.OrderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());

        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());

        using var pod = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{shipment.Stops[0].Id}/pod",
            new
            {
                lines = new[]
                {
                    new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = 3m }
                },
                signerName = "Chef Lee",
                photoFileIds = Array.Empty<Guid>(),
                geo = (string?)null
            });
        pod.StatusCode.ShouldBe(HttpStatusCode.OK, await pod.Content.ReadAsStringAsync());
        var signed = await pod.DeserializeAsync<ShipmentDto>();
        signed.Returns.ShouldHaveSingleItem().Quantity.ShouldBe(2m);

        using var received = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        var order = await received.DeserializeAsync<SalesOrderDto>();
        order.Status.ShouldBe("Received");
        order.Lines[0].DeliveredQty.ShouldBe(3m);
        order.Lines[0].ReturnedQty.ShouldBe(2m);
        order.Lines[0].VarianceReason.ShouldBe("partial-reject");

        decimal availableAfter = await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId);
        availableAfter.ShouldBe(availableBefore + 2m);

        using var createPutaway = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = packed.WarehouseId,
                zone = "Ambient",
                productId = packed.ProductId,
                lotId = packed.LotId,
                quantity = 2m,
                source = "ReturnOnTruck"
            });
        createPutaway.StatusCode.ShouldBe(HttpStatusCode.OK, await createPutaway.Content.ReadAsStringAsync());
        var putaway = await createPutaway.DeserializeAsync<PutawayTaskDto>();
        putaway.Status.ShouldBe("Pending");
        putaway.Source.ShouldBe("ReturnOnTruck");

        using var shrink = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/shrinkage",
            new
            {
                warehouseId = packed.WarehouseId,
                zone = "Ambient",
                productId = packed.ProductId,
                lotId = packed.LotId,
                quantity = 2m,
                reason = "return-damage",
                photoFileIds = Array.Empty<Guid>()
            });
        shrink.StatusCode.ShouldBe(HttpStatusCode.OK, await shrink.Content.ReadAsStringAsync());
        (await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId)).ShouldBe(availableBefore);

        using var kpis = await client.GetAsync(
            $"{TestConstants.OpsBasePath}/kpis?date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        kpis.StatusCode.ShouldBe(HttpStatusCode.OK, await kpis.Content.ReadAsStringAsync());
        var board = await kpis.DeserializeAsync<OpsKpisDto>();
        board.LossQty.ShouldBeGreaterThanOrEqualTo(2m);
        board.ShrinkageRate.ShouldBeGreaterThan(0m);
    }

    private sealed record PackedOrder(
        Guid WarehouseId,
        Guid StoreId,
        Guid ProductId,
        Guid OrderId,
        Guid LotId,
        DateOnly BusinessDate,
        decimal Quantity);

    private static async Task<PackedOrder> PackOrderAsync(HttpClient client, decimal orderQty)
    {
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid lotId = await ReceiveAsync(client, warehouse.Id, productId, $"LOT-{Guid.NewGuid():N}"[..12], 20m, today.AddDays(8));

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        await PutCartAsync(client, storeId, productId, orderQty);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff",
            new { });
        cutoff.StatusCode.ShouldBe(HttpStatusCode.OK, await cutoff.Content.ReadAsStringAsync());
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();

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

        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = lotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        using var packed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await packed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Packed");

        return new PackedOrder(
            warehouse.Id, storeId, productId, orderId, lotId, cutoffResult.BusinessDate, orderQty);
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

    private static async Task PutCartAsync(HttpClient client, Guid storeId, Guid productId, decimal qty)
    {
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = qty } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        var code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brandResp.StatusCode.ShouldBe(HttpStatusCode.OK, await brandResp.Content.ReadAsStringAsync());

        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Cat"), description = (string?)null, parentCategoryId = (Guid?)null });
        categoryResp.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResp.Content.ReadAsStringAsync());

        var sku = $"LG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name = Unique("Cod"),
                description = "Logistics SKU",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = 9.5m,
                priceCurrency = "USD",
                stock = 0,
            });
        productResp.StatusCode.ShouldBe(HttpStatusCode.OK, await productResp.Content.ReadAsStringAsync());
        return await productResp.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> ReceiveAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string lotNo,
        decimal quantity,
        DateOnly expiry)
    {
        using var receive = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            new
            {
                warehouseId,
                zone = "Ambient",
                productId,
                lotNo,
                expiryDate = expiry,
                quantity,
                idempotencyKey = $"recv-{Guid.NewGuid():N}",
                manufacturedOn = (DateOnly?)null,
                origin = "Boston",
            });
        receive.StatusCode.ShouldBe(HttpStatusCode.OK, await receive.Content.ReadAsStringAsync());
        return await receive.DeserializeAsync<Guid>();
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
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone=Ambient");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.DeserializeAsync<AvailableQtyDto>()).Available;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
