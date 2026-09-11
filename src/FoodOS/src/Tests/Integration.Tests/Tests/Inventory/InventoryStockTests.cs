using FSH.Modules.Inventory.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Inventory;

/// <summary>
/// Warehouse create + receive + ATP for the Inventory skeleton.
/// Lot quantity lives in inventory schema (warehouse × zone × lot), not Catalog.Product.Stock.
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
        warehouse.Zones.Select(z => z.Kind).ShouldBe(["Ambient", "Chilled", "Frozen"], ignoreOrder: true);
    }

    [Fact]
    public async Task ReceiveStock_Should_IncreaseAvailableQty_And_HonorIdempotencyKey()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = Guid.CreateVersion7();
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var idempotencyKey = $"recv-{Guid.NewGuid():N}";

        var receiveBody = new
        {
            warehouseId = warehouse.Id,
            zone = "Ambient",
            productId,
            lotNo = "LOT-A1",
            expiryDate = expiry,
            quantity = 10m,
            idempotencyKey,
            manufacturedOn = (DateOnly?)null,
            origin = "Boston",
        };

        using var first = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            receiveBody);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var lotId = await first.DeserializeAsync<Guid>();
        lotId.ShouldNotBe(Guid.Empty);

        using var replay = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            receiveBody);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await replay.DeserializeAsync<Guid>()).ShouldBe(lotId);

        var available = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        available.Available.ShouldBe(10m);
        available.ZoneKind.ShouldBe("Ambient");

        using var second = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            new
            {
                warehouseId = warehouse.Id,
                zone = "Ambient",
                productId,
                lotNo = "LOT-A1",
                expiryDate = expiry,
                quantity = 5m,
                idempotencyKey = $"recv-{Guid.NewGuid():N}",
                manufacturedOn = (DateOnly?)null,
                origin = "Boston",
            });
        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());

        var afterSecond = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterSecond.Available.ShouldBe(15m);
    }

    [Fact]
    public async Task GetWarehouseById_Should_Return404_When_OwnedByDifferentTenant()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(rootClient);

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"inv-{uniqueId}");

        using var crossGet = await otherClient.GetAsync(
            $"{TestConstants.InventoryBasePath}/warehouses/{warehouse.Id}");
        crossGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.InventoryBasePath}/warehouses/{warehouse.Id}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InventoryEndpoints_Should_Return401_When_Anonymous()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    private static async Task<AvailableQtyDto> GetAvailableAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string zone)
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

        for (int i = 0; i < 60; i++)
        {
            using var statusResponse = await rootClient.GetAsync(
                $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
            if (statusResponse.IsSuccessStatusCode)
            {
                var content = await statusResponse.Content.ReadAsStringAsync();
                if (content.Contains("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return await _auth.CreateAuthenticatedClientAsync(
                        adminEmail, TestConstants.DefaultPassword, tenantId);
                }

                if (content.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Tenant {tenantId} provisioning failed: {content}");
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Tenant {tenantId} provisioning did not complete.");
    }
}
