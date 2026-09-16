using FSH.Modules.Inventory.Contracts.Dtos;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
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
    private readonly FshWebApplicationFactory _factory;

    public WarehouseWaveTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
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
        cutoffResult.WavesGenerated.ShouldBe(1);

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

        using var unprivileged = await CreateUnprivilegedOperatorAsync();
        using var deniedRelease = await unprivileged.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release", new { });
        deniedRelease.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

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

        using var deniedRead = await unprivileged.GetAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
        deniedRead.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var deniedMine = await unprivileged.GetAsync($"{TestConstants.WarehouseBasePath}/pick-tasks/mine");
        deniedMine.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var deniedPick = await unprivileged.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = earlyLotId });
        deniedPick.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var deniedPack = await unprivileged.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/pack", new { });
        deniedPack.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var unchangedWave = await client.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
        (await unchangedWave.DeserializeAsync<WaveDto>()).Tasks.ShouldHaveSingleItem().Status.ShouldBe("Pending");

        using var getPicking = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await getPicking.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Picking");

        using var pickerA = await CreateUnprivilegedOperatorAsync(
            WarehousePermissions.Picks.View, WarehousePermissions.Picks.Confirm, WarehousePermissions.Waves.View);
        using var pickerB = await CreateUnprivilegedOperatorAsync(
            WarehousePermissions.Picks.View, WarehousePermissions.Picks.Confirm, WarehousePermissions.Waves.View);
        Guid pickerAId = await WaveAssignments.UserIdAsync(pickerA);
        Guid pickerBId = await WaveAssignments.UserIdAsync(pickerB);
        using var supervisor = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Waves.Assign, WarehousePermissions.Waves.View);
        string assignUrl = $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/assign";
        string mineUrl = $"{TestConstants.WarehouseBasePath}/pick-tasks/mine?warehouseId={warehouse.Id}";
        using var unassigned = await pickerA.GetAsync(mineUrl);
        (await unassigned.DeserializeAsync<List<PickTaskDto>>()).ShouldBeEmpty();
        using var unassignedConfirm = await pickerA.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = earlyLotId });
        unassignedConfirm.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var selfAssign = await pickerA.PostAsJsonAsync(assignUrl, new { pickerUserId = pickerAId });
        selfAssign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var invalidAssign = await client.PostAsJsonAsync(assignUrl, new { pickerUserId = Guid.NewGuid() });
        invalidAssign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var unqualifiedAssign = await client.PostAsJsonAsync(assignUrl,
            new { pickerUserId = await WaveAssignments.UserIdAsync(unprivileged) });
        unqualifiedAssign.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var assigned = await supervisor.PostAsJsonAsync(assignUrl, new { pickerUserId = pickerAId });
        assigned.StatusCode.ShouldBe(HttpStatusCode.OK, await assigned.Content.ReadAsStringAsync());
        (await assigned.DeserializeAsync<WaveDto>()).AssignedPickerUserId.ShouldBe(pickerAId);
        using var duplicateAssign = await client.PostAsJsonAsync(assignUrl, new { pickerUserId = pickerAId });
        duplicateAssign.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var reassign = await client.PostAsJsonAsync(assignUrl, new { pickerUserId = pickerBId });
        reassign.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var mineA = await pickerA.GetAsync(mineUrl);
        (await mineA.DeserializeAsync<List<PickTaskDto>>()).ShouldHaveSingleItem().Id.ShouldBe(task.Id);
        using var mineB = await pickerB.GetAsync(mineUrl);
        (await mineB.DeserializeAsync<List<PickTaskDto>>()).ShouldBeEmpty();
        using var foreignWave = await pickerB.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
        foreignWave.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignList = await pickerB.GetAsync($"{TestConstants.WarehouseBasePath}/waves?warehouseId={warehouse.Id}");
        (await foreignList.DeserializeAsync<List<WaveDto>>()).ShouldBeEmpty();
        using var ownWave = await pickerA.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
        ownWave.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var other in new[] { pickerB, client })
        {
            using var forbiddenConfirm = await other.PostAsJsonAsync(
                $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = earlyLotId });
            forbiddenConfirm.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var wrong = await pickerA.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = isolatedLotId });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await wrong.Content.ReadAsStringAsync());

        using var stillPicking = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await stillPicking.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Picking");

        using var confirm = await pickerA.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = earlyLotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        var picked = await confirm.DeserializeAsync<PickTaskDto>();
        picked.Status.ShouldBe("Picked");

        using var repeat = await pickerA.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = earlyLotId });
        repeat.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var foreignRepeat = await pickerB.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = earlyLotId });
        foreignRepeat.StatusCode.ShouldBe(HttpStatusCode.NotFound);

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

    private Task<HttpClient> CreateUnprivilegedOperatorAsync(params string[] permissions)
        => OperatorTestUsers.CreateOperatorAsync(_factory, permissions);

    [Fact]
    public async Task Assignment_Should_RejectInactivePicker_And_KeepOneWinner_WhenSupervisorsRace()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        using var pickerA = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Picks.View, WarehousePermissions.Picks.Confirm);
        using var pickerB = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Picks.View, WarehousePermissions.Picks.Confirm);
        Guid userA = await WaveAssignments.UserIdAsync(pickerA);
        Guid userB = await WaveAssignments.UserIdAsync(pickerB);
        var warehouse = await CreateWarehouseAsync(admin);
        var wave = Wave.Create($"RACE{Guid.NewGuid():N}", Guid.NewGuid(), warehouse.Id,
            warehouse.Zones[0].Id, "Ambient", DateOnly.FromDateTime(DateTime.UtcNow));
        using (var scope = _factory.Services.CreateScope())
        {
            var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
                .GetAsync(TestConstants.RootTenantId);
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
                new MultiTenantContext<AppTenantInfo>(tenant);
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            db.Waves.Add(wave);
            await db.SaveChangesAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
            var user = await users.FindByIdAsync(userA.ToString());
            user.ShouldNotBeNull();
            user.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }
        string url = $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/assign";
        using var inactive = await admin.PostAsJsonAsync(url, new { pickerUserId = userA });
        inactive.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var empty = await admin.PostAsJsonAsync(url, new { pickerUserId = Guid.Empty });
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var pickerC = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Picks.View, WarehousePermissions.Picks.Confirm);
        Guid userC = await WaveAssignments.UserIdAsync(pickerC);
        using var supervisorA = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Waves.Assign, WarehousePermissions.Waves.View);
        using var supervisorB = await CreateUnprivilegedOperatorAsync(WarehousePermissions.Waves.Assign, WarehousePermissions.Waves.View);
        var responses = await Task.WhenAll(
            supervisorA.PostAsJsonAsync(url, new { pickerUserId = userB }),
            supervisorB.PostAsJsonAsync(url, new { pickerUserId = userC }));
        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
            var winner = await responses.Single(r => r.IsSuccessStatusCode).DeserializeAsync<WaveDto>();
            using var persisted = await admin.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
            (await persisted.DeserializeAsync<WaveDto>()).AssignedPickerUserId.ShouldBe(winner.AssignedPickerUserId);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
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
        await WaveAssignments.AssignToSelfAsync(client, wave.Id);
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

    [Fact]
    public async Task GenerateWave_Should_SplitByZoneAndStoreRoute()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-RT", 20m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)));

        var orgId = await CreateCustomerOrgAsync(client);
        var routeA = Guid.CreateVersion7();
        var routeB = Guid.CreateVersion7();
        var storeA = await CreateStoreAsync(client, orgId, warehouse.Id, routeA);
        var storeB = await CreateStoreAsync(client, orgId, warehouse.Id, routeB);
        await PutCartAsync(client, storeA, productId, 2m);
        await PutCartAsync(client, storeB, productId, 3m);

        using var placeA = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/orders", new { storeId = storeA });
        placeA.StatusCode.ShouldBe(HttpStatusCode.OK, await placeA.Content.ReadAsStringAsync());
        using var placeB = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/orders", new { storeId = storeB });
        placeB.StatusCode.ShouldBe(HttpStatusCode.OK, await placeB.Content.ReadAsStringAsync());

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff", new { });
        cutoff.StatusCode.ShouldBe(HttpStatusCode.OK, await cutoff.Content.ReadAsStringAsync());
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();
        cutoffResult.WavesGenerated.ShouldBe(2);

        using var listed = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/waves?warehouseId={warehouse.Id}&businessDate={cutoffResult.BusinessDate:yyyy-MM-dd}");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        var autoWaves = await listed.DeserializeAsync<List<WaveDto>>();
        autoWaves.Count.ShouldBe(2);
        autoWaves.ShouldAllBe(w => w.Status == "Draft");

        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        generate.StatusCode.ShouldBe(HttpStatusCode.OK, await generate.Content.ReadAsStringAsync());
        var waves = await generate.DeserializeAsync<List<WaveDto>>();
        waves.Count.ShouldBe(2);
        waves.Select(w => w.Zone).Distinct().ShouldBe(["Ambient"]);
        waves.Select(w => w.RouteId).ShouldBe([routeA, routeB], ignoreOrder: true);
        waves.Sum(w => w.Tasks.Sum(t => t.Quantity)).ShouldBe(5m);

        using var again = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        var replay = await again.DeserializeAsync<List<WaveDto>>();
        replay.Select(w => w.Id).OrderBy(id => id).ShouldBe(waves.Select(w => w.Id).OrderBy(id => id));
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

    private static async Task<Guid> CreateStoreAsync(
        HttpClient client,
        Guid orgId,
        Guid warehouseId,
        Guid? defaultRouteId = null)
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
                defaultRouteId,
                deliveryWindow = "05:00-08:00",
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
