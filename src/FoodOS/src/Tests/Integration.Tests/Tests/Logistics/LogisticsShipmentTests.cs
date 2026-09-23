using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Logistics;

/// <summary>
/// External-WMS mode blocks legacy shipment and POD execution while retaining safe logistics queries and master data.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class LogisticsShipmentTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public LogisticsShipmentTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task CreateLoadDepartAndPod_Should_FailClosed_WithoutLocalShipmentState()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateLogisticsSetupAsync(client);

        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                setup.RouteId,
                setup.WarehouseId,
                setup.VehicleId,
                setup.DriverId,
                businessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        await AssertBlockedAsync(create);

        Guid shipmentId = Guid.NewGuid();
        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipmentId}/load",
            new { orderIds = new[] { Guid.NewGuid() } });
        await AssertBlockedAsync(load);
        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipmentId}/depart", new { });
        await AssertBlockedAsync(depart);
        using var pod = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{Guid.NewGuid()}/pod",
            CreatePodPayload(setup.ProductId, 1m));
        await AssertBlockedAsync(pod);

        await AssertNoShipmentExecutionStateAsync();
    }

    [Fact]
    public async Task DriverQueries_Should_RemainScoped_WhenShipmentCreationIsWmsBlocked()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var role = await CreateRoleAsync(admin, $"DriverScope-{suffix}");
        await SetRolePermissionsAsync(
            admin,
            role.Id,
            LogisticsPermissions.Shipments.ViewAssigned,
            LogisticsPermissions.ProofOfDelivery.Confirm);

        var driverAUser = await CreateActiveUserAsync($"driver-a-{suffix}");
        var driverBUser = await CreateActiveUserAsync($"driver-b-{suffix}");
        await AssignRoleAsync(admin, driverAUser.UserId, role.Name);
        await AssignRoleAsync(admin, driverBUser.UserId, role.Name);
        using var driverA = await _auth.CreateAuthenticatedClientAsync(driverAUser.Email, driverAUser.Password);
        using var driverB = await _auth.CreateAuthenticatedClientAsync(driverBUser.Email, driverBUser.Password);

        var setup = await CreateLogisticsSetupAsync(admin, Guid.Parse(driverAUser.UserId));
        using var create = await admin.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                setup.RouteId,
                setup.WarehouseId,
                setup.VehicleId,
                setup.DriverId,
                businessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        await AssertBlockedAsync(create);

        foreach (var driver in new[] { driverA, driverB })
        {
            using var list = await driver.GetAsync($"{TestConstants.LogisticsBasePath}/shipments/mine");
            list.StatusCode.ShouldBe(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
            (await list.DeserializeAsync<IReadOnlyList<ShipmentDto>>()).ShouldBeEmpty();
            using var detail = await driver.GetAsync(
                $"{TestConstants.LogisticsBasePath}/shipments/mine/{Guid.NewGuid()}");
            detail.StatusCode.ShouldBe(HttpStatusCode.NotFound, await detail.Content.ReadAsStringAsync());
        }

        using var unrestricted = await driverA.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{Guid.NewGuid()}");
        unrestricted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertNoShipmentExecutionStateAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialRejectRetry_Should_RemainBlocked_WithoutPodOrReturnProjection(bool retry)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateLogisticsSetupAsync(client);
        decimal availableBefore = await GetAvailableAsync(client, setup.WarehouseId, setup.ProductId);
        string podUrl = $"{TestConstants.LogisticsBasePath}/stops/{Guid.NewGuid()}/pod";
        var payload = CreatePodPayload(setup.ProductId, 3m);

        using var first = await client.PostAsJsonAsync(podUrl, payload);
        await AssertBlockedAsync(first);
        if (retry)
        {
            using var second = await client.PostAsJsonAsync(podUrl, payload);
            await AssertBlockedAsync(second);
        }

        (await GetAvailableAsync(client, setup.WarehouseId, setup.ProductId)).ShouldBe(availableBefore);
        await AssertNoShipmentExecutionStateAsync();
    }

    private async Task AssertNoShipmentExecutionStateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<LogisticsDbContext>();
        (await db.Shipments.AnyAsync()).ShouldBeFalse();
        (await db.ShipmentStops.AnyAsync()).ShouldBeFalse();
        (await db.ProofOfDeliveries.AnyAsync()).ShouldBeFalse();
        (await db.ReturnsOnTruck.AnyAsync()).ShouldBeFalse();
    }

    private static object CreatePodPayload(Guid productId, decimal signedQty) => new
    {
        lines = new[]
        {
            new { orderLineId = Guid.NewGuid(), lotId = productId, signedQty },
        },
        signerName = "External WMS boundary",
        photoFileIds = Array.Empty<Guid>(),
        geo = (string?)null,
    };

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private static async Task<LogisticsSetup> CreateLogisticsSetupAsync(HttpClient client, Guid? driverUserId = null)
    {
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        var vehicleId = await CreateVehicleAsync(client);
        var driverId = await CreateDriverAsync(
            client,
            driverUserId ?? await WaveAssignments.UserIdAsync(client));
        var routeId = await CreateRouteAsync(client, warehouse.Id, storeId);
        return new LogisticsSetup(warehouse.Id, productId, vehicleId, driverId, routeId);
    }

    private sealed record LogisticsSetup(
        Guid WarehouseId,
        Guid ProductId,
        Guid VehicleId,
        Guid DriverId,
        Guid RouteId);

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

    private static async Task<Guid> CreateDriverAsync(HttpClient client, Guid? userId = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/drivers",
            new { userId = userId ?? Guid.CreateVersion7(), phone = "+16175550100" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<RoleDto> CreateRoleAsync(HttpClient adminClient, string name)
    {
        using var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles", new
        {
            id = string.Empty,
            name,
            description = "driver assignment isolation test role",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<RoleDto>();
    }

    private static async Task SetRolePermissionsAsync(HttpClient adminClient, string roleId, params string[] permissions)
    {
        using var response = await adminClient.PutAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/{roleId}/permissions",
            new { roleId, permissions });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssignRoleAsync(HttpClient adminClient, string userId, string roleName)
    {
        using var response = await adminClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{userId}/roles",
            new { userId, userRoles = new[] { new { roleName, enabled = true } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<(string Email, string Password, string UserId)> CreateActiveUserAsync(string handle)
    {
        const string password = TestConstants.DefaultPassword;
        string email = $"{handle}@example.com";
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Driver",
            LastName = "Scope",
            Email = email,
            UserName = handle,
            EmailConfirmed = true,
            IsActive = true,
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Description)));
        return (email, password, user.Id);
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
