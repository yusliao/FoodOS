using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Warehouse;

/// <summary>
/// 剧本 A 步 3–6 + 剧本 B 步 4：截单锁单、FEFO 波次、隔离批跳过、PDA 扫错拒、扫对 Packed。
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class WarehouseWaveTests
{
    private readonly AuthHelper _auth;

    public WarehouseWaveTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task CutoffWaveAndPick_Should_UseFefo_SkipIsolated_And_RejectWrongLot()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid isolatedLotId = await ReceiveAsync(client, warehouse.Id, productId, "LOT-ISO", 10m, today.AddDays(2));
        Guid earlyLotId = await ReceiveAsync(client, warehouse.Id, productId, "LOT-EARLY", 10m, today.AddDays(5));
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-LATE", 10m, today.AddDays(40));

        using var isolate = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/isolate",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Ambient",
                lotId = isolatedLotId,
                quantity = 10m,
                idempotencyKey = $"iso-{Guid.NewGuid():N}",
                reason = "qc-fail"
            });
        isolate.StatusCode.ShouldBe(HttpStatusCode.OK, await isolate.Content.ReadAsStringAsync());

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        await PutCartAsync(client, storeId, productId, 7m);

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
        cutoffResult.OrdersLocked.ShouldBe(1);

        using var getLocked = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var locked = await getLocked.DeserializeAsync<SalesOrderDto>();
        locked.Status.ShouldBe("Planned");
        locked.BusinessDate.ShouldBe(cutoffResult.BusinessDate);

        using var amend = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId, quantity = 3m } } });
        amend.StatusCode.ShouldBe(HttpStatusCode.Conflict, await amend.Content.ReadAsStringAsync());

        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        generate.StatusCode.ShouldBe(HttpStatusCode.OK, await generate.Content.ReadAsStringAsync());
        var drafts = await generate.DeserializeAsync<List<WaveDto>>();
        var wave = drafts.ShouldHaveSingleItem();
        wave.Status.ShouldBe("Draft");

        using var release = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release",
            new { });
        release.StatusCode.ShouldBe(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        var released = await release.DeserializeAsync<WaveDto>();
        released.Status.ShouldBe("Picking");
        var task = released.Tasks.ShouldHaveSingleItem();
        task.LotId.ShouldBe(earlyLotId);
        task.LotNo.ShouldBe("LOT-EARLY");
        released.Tasks.ShouldNotContain(t => t.LotId == isolatedLotId);

        using var getPicking = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await getPicking.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Picking");

        using var wrong = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = isolatedLotId });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await wrong.Content.ReadAsStringAsync());

        using var stillPicking = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await stillPicking.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Picking");

        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = earlyLotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        var picked = await confirm.DeserializeAsync<PickTaskDto>();
        picked.Status.ShouldBe("Picked");

        using var getPacked = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await getPacked.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Packed");

        var nextStoreId = await CreateStoreAsync(client, orgId, warehouse.Id);
        await PutCartAsync(client, nextStoreId, productId, 1m);
        using var nextPlace = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId = nextStoreId });
        nextPlace.StatusCode.ShouldBe(HttpStatusCode.OK, await nextPlace.Content.ReadAsStringAsync());
        var nextOrderId = await nextPlace.DeserializeAsync<Guid>();
        using var getNext = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{nextOrderId}");
        var nextOrder = await getNext.DeserializeAsync<SalesOrderDto>();
        nextOrder.Status.ShouldBe("Reserved");
        nextOrder.BusinessDate.ShouldNotBe(cutoffResult.BusinessDate);
    }

    [Fact]
    public async Task WaveShortPick_Should_WriteShortageReason_OnOrderLine_And_FulfillOtherLine()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var shortProductId = await CreateProductAsync(client);
        var fullProductId = await CreateProductAsync(client);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid shortLotId = await ReceiveAsync(client, warehouse.Id, shortProductId, "LOT-SH", 10m, today.AddDays(8));
        await ReceiveAsync(client, warehouse.Id, fullProductId, "LOT-OK", 10m, today.AddDays(8));

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new
            {
                storeId,
                lines = new[]
                {
                    new { productId = shortProductId, quantity = 8m },
                    new { productId = fullProductId, quantity = 8m }
                }
            });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/orders", new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var shrink = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/shrinkage",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Ambient",
                productId = shortProductId,
                lotId = shortLotId,
                quantity = 6m,
                reason = "damage",
                photoFileIds = Array.Empty<Guid>()
            });
        shrink.StatusCode.ShouldBe(HttpStatusCode.OK, await shrink.Content.ReadAsStringAsync());

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff", new { });
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();
        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        var wave = (await generate.DeserializeAsync<List<WaveDto>>()).ShouldHaveSingleItem();
        using var release = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release", new { });
        var released = await release.DeserializeAsync<WaveDto>();

        var shortTask = released.Tasks.First(t => t.ProductId == shortProductId);
        shortTask.ShortageQty.ShouldBe(4m);
        shortTask.LotId.ShouldNotBeNull();
        var fullTask = released.Tasks.First(t => t.ProductId == fullProductId);
        fullTask.ShortageQty.ShouldBe(0m);
        fullTask.LotId.ShouldNotBeNull();

        using var confirmShort = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{shortTask.Id}/confirm",
            new { scannedLotId = shortTask.LotId });
        confirmShort.StatusCode.ShouldBe(HttpStatusCode.OK, await confirmShort.Content.ReadAsStringAsync());
        using var confirmFull = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{fullTask.Id}/confirm",
            new { scannedLotId = fullTask.LotId });
        confirmFull.StatusCode.ShouldBe(HttpStatusCode.OK, await confirmFull.Content.ReadAsStringAsync());

        using var getOrder = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var order = await getOrder.DeserializeAsync<SalesOrderDto>();
        order.Status.ShouldBe("Packed");
        order.Lines.ShouldContain(l => l.ProductId == shortProductId);
        var shortLine = order.Lines.First(l => l.ProductId == shortProductId);
        shortLine.ShortageQty.ShouldBe(4m);
        shortLine.ShortageReason.ShouldBe("insufficient-stock");
        var fullLine = order.Lines.First(l => l.ProductId == fullProductId);
        fullLine.ShortageQty.ShouldBe(0m);
        fullLine.ShortageReason.ShouldBeNull();
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

        var sku = $"WV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name = Unique("Cod"),
                description = "Wave SKU",
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

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
