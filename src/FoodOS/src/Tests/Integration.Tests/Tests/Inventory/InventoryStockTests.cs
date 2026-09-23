using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Data;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Inventory;

/// <summary>
/// External-WMS mode keeps warehouse and stock projections readable, but every legacy physical-stock write fails closed.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class InventoryStockTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public InventoryStockTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task CreateWarehouse_Should_SeedThreeZonesAndTrialClock()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);

        warehouse.TimeZoneId.ShouldBe("America/New_York");
        warehouse.Clock.TimeZoneId.ShouldBe("America/New_York");
        warehouse.Clock.CutoffLocal.ShouldBe("16:00");
        warehouse.Zones.Count.ShouldBe(3);
        warehouse.Zones.Select(zone => zone.Kind)
            .ShouldBe(["Ambient", "Chilled", "Frozen"], ignoreOrder: true);
    }

    [Fact]
    public async Task ReceiveStock_Should_BeBlockedAcrossRetries_WithoutInventoryWrites()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();
        var body = ReceiveBody(warehouse.Id, productId, "LOT-A1", $"recv-{Guid.NewGuid():N}");

        using var first = await client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/receive", body);
        await AssertBlockedAsync(first);
        using var replay = await client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/receive", body);
        await AssertBlockedAsync(replay);
        using var changedKey = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            ReceiveBody(warehouse.Id, productId, "LOT-A1", $"recv-{Guid.NewGuid():N}"));
        await AssertBlockedAsync(changedKey);

        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
        await AssertNoStockWritesAsync(productId);
    }

    [Fact]
    public async Task ReserveAndUnreserve_Should_BeBlockedWithoutReservationOrAtpChange()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();
        var reserveBody = new
        {
            warehouseId = warehouse.Id,
            zone = "Ambient",
            productId,
            quantity = 6m,
            orderId = Guid.CreateVersion7(),
            idempotencyKey = $"rsv-{Guid.NewGuid():N}",
            orderLineId = Guid.CreateVersion7(),
        };

        using var reserve = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/reserve", reserveBody);
        await AssertBlockedAsync(reserve);
        using var replay = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/reserve", reserveBody);
        await AssertBlockedAsync(replay);
        using var unreserve = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/unreserve",
            new { reservationId = Guid.NewGuid(), idempotencyKey = $"unr-{Guid.NewGuid():N}" });
        await AssertBlockedAsync(unreserve);

        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
        await AssertNoStockWritesAsync(productId);
    }

    [Fact]
    public async Task ConcurrentReserve_Should_AllFailClosedWithoutReservations()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();
        object ReserveBody() => new
        {
            warehouseId = warehouse.Id,
            zone = "Ambient",
            productId,
            quantity = 8m,
            orderId = Guid.CreateVersion7(),
            idempotencyKey = $"rsv-{Guid.NewGuid():N}",
            orderLineId = (Guid?)null,
        };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/reserve", ReserveBody()),
            client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/reserve", ReserveBody()));
        try
        {
            responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
        await AssertNoStockWritesAsync(productId);
    }

    [Fact]
    public async Task IsolateStock_Should_BeBlockedWithoutCreatingLotOrBalance()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();

        using var isolate = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/isolate",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Ambient",
                lotId = Guid.NewGuid(),
                quantity = 8m,
                idempotencyKey = $"iso-{Guid.NewGuid():N}",
                reason = "qc-fail",
            });
        await AssertBlockedAsync(isolate);

        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
        await AssertNoStockWritesAsync(productId);
    }

    [Fact]
    public async Task GetWarehouseById_Should_Return403_When_CustomerRequestsOperatorWarehouse()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(rootClient);
        using var otherClient = await ProvisionTenantClientAsync(
            rootClient, $"inv-{Guid.NewGuid().ToString("N")[..8]}");

        using var crossGet = await otherClient.GetAsync(
            $"{TestConstants.InventoryBasePath}/warehouses/{warehouse.Id}");
        crossGet.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.InventoryBasePath}/warehouses/{warehouse.Id}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CountAdjustment_Should_BeBlockedWithoutLotBalanceOrLedger()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();
        var body = new
        {
            warehouseId = warehouse.Id,
            zone = "Ambient",
            productId,
            lotId = Guid.NewGuid(),
            countedAvailable = 7m,
            idempotencyKey = $"cnt-{Guid.NewGuid():N}",
        };

        using var count = await client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/count", body);
        await AssertBlockedAsync(count);
        using var replay = await client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/count", body);
        await AssertBlockedAsync(replay);

        using var lots = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/lots?warehouseId={warehouse.Id}&productId={productId}");
        lots.StatusCode.ShouldBe(HttpStatusCode.OK, await lots.Content.ReadAsStringAsync());
        (await lots.DeserializeAsync<PagedResponse<LotDto>>()).Items.ShouldBeEmpty();
        await AssertNoStockWritesAsync(productId);
    }

    [Fact]
    public async Task InventoryEndpoints_Should_Return401_When_Anonymous()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task AssertNoStockWritesAsync(Guid productId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await db.Lots.AnyAsync(lot => lot.ProductId == productId)).ShouldBeFalse();
        (await db.LotBalances.AnyAsync(balance => balance.ProductId == productId)).ShouldBeFalse();
        (await db.Reservations.AnyAsync(reservation => reservation.ProductId == productId)).ShouldBeFalse();
        (await db.InventoryTransactions.AnyAsync(transaction => transaction.ProductId == productId)).ShouldBeFalse();
    }

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private static object ReceiveBody(Guid warehouseId, Guid productId, string lotNo, string idempotencyKey) => new
    {
        warehouseId,
        zone = "Ambient",
        productId,
        lotNo,
        expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        quantity = 10m,
        idempotencyKey,
        manufacturedOn = (DateOnly?)null,
        origin = "Boston",
    };

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

    private static async Task<AvailableQtyDto> GetAvailableAsync(
        HttpClient client, Guid warehouseId, Guid productId, string zone)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone={zone}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<AvailableQtyDto>();
    }

    private async Task<HttpClient> ProvisionTenantClientAsync(HttpClient rootClient, string tenantId)
    {
        var adminEmail = $"{tenantId}-admin@tenant.com";
        using var createTenant = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer"
        });
        createTenant.StatusCode.ShouldBe(HttpStatusCode.Created, await createTenant.Content.ReadAsStringAsync());

        for (int attempt = 0; attempt < 60; attempt++)
        {
            using var statusResponse = await rootClient.GetAsync(
                $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
            if (statusResponse.IsSuccessStatusCode)
            {
                var content = await statusResponse.Content.ReadAsStringAsync();
                if (content.Contains("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    HttpRequestException? lastAuthError = null;
                    for (int retry = 0; retry < 10; retry++)
                    {
                        try
                        {
                            return await _auth.CreateAuthenticatedClientAsync(
                                adminEmail, TestConstants.DefaultPassword, tenantId);
                        }
                        catch (HttpRequestException ex)
                        {
                            lastAuthError = ex;
                            await Task.Delay(500);
                        }
                    }

                    if (lastAuthError is not null) throw lastAuthError;
                }

                if (content.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Tenant {tenantId} provisioning failed: {content}");
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Tenant {tenantId} provisioning did not complete.");
    }
}
