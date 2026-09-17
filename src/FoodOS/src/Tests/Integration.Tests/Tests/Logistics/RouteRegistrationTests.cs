using FSH.Modules.Logistics.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using Mediator;

namespace Integration.Tests.Tests.Logistics;

[Collection(FshCollectionDefinition.Name)]
public sealed class RouteRegistrationTests(FshWebApplicationFactory factory)
{
    [Theory]
    [InlineData("warehouse", HttpStatusCode.NotFound)]
    [InlineData("store", HttpStatusCode.NotFound)]
    [InlineData("vehicle", HttpStatusCode.NotFound)]
    [InlineData("duplicate", HttpStatusCode.BadRequest)]
    public async Task Create_Should_RejectInvalidReferences_WithoutSaving(string kind, HttpStatusCode expected)
    {
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        var (warehouseId, stores) = await CreateReferencesAsync(client);
        if (kind == "warehouse") warehouseId = Guid.NewGuid();
        if (kind == "store") stores = [Guid.NewGuid()];
        if (kind == "duplicate") stores = [stores[0], stores[0]];
        var code = Code();
        using var response = await client.PostAsJsonAsync("/api/v1/logistics/routes", new {
            warehouseId, code, storeIds = stores, defaultVehicleId = kind == "vehicle" ? (Guid?)Guid.NewGuid() : null,
        });
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        using var list = await client.GetAsync($"/api/v1/logistics/routes?warehouseId={warehouseId}");
        (await list.DeserializeAsync<List<RouteDto>>()).ShouldNotContain(route => route.Code == code);
    }

    [Fact]
    public async Task Create_Should_PreserveStoreOrder_AndReturnOriginalRecordOnReplay()
    {
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        var (warehouseId, stores) = await CreateReferencesAsync(client);
        var vehicleId = await CreateAsync(client, "logistics/vehicles", new { plate = Code(), compartmentZones = "Ambient", payloadKg = 1000 });
        var code = Code();
        var id = await CreateAsync(client, "logistics/routes", new { warehouseId, code, storeIds = stores, defaultVehicleId = vehicleId });
        var replay = await CreateAsync(client, "logistics/routes", new { warehouseId, code, storeIds = stores.Reverse().ToArray(), defaultVehicleId = (Guid?)null });
        replay.ShouldBe(id);
        using var get = await client.GetAsync($"/api/v1/logistics/routes/{id}");
        var route = await get.DeserializeAsync<RouteDto>();
        route.StoreIds.ShouldBe(stores);
        route.DefaultVehicleId.ShouldBe(vehicleId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InternalRegistration_Should_RejectMissingOrCustomerContext(bool customer)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(customer ? new AppTenantInfo("route-customer", "route-customer") : null);
        var error = await Should.ThrowAsync<CustomException>(async () => await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateRouteCommand(Guid.NewGuid(), Code(), [Guid.NewGuid()], null)));
        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Should_AllowNoDefaultVehicle()
    {
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        var (warehouseId, stores) = await CreateReferencesAsync(client);
        var id = await CreateAsync(client, "logistics/routes", new { warehouseId, code = Code(), storeIds = stores });
        using var get = await client.GetAsync($"/api/v1/logistics/routes/{id}");
        (await get.DeserializeAsync<RouteDto>()).DefaultVehicleId.ShouldBeNull();
    }

    private static async Task<(Guid WarehouseId, Guid[] Stores)> CreateReferencesAsync(HttpClient client)
    {
        var warehouse = await CreateAsync(client, "inventory/warehouses", new { code = Code(), name = "Route warehouse", city = "Boston" });
        var org = await CreateAsync(client, "ordering/customer-orgs", new { code = Code(), name = "Route customer" });
        var stores = new List<Guid>();
        for (int i = 0; i < 2; i++) stores.Add(await CreateAsync(client, "ordering/stores", new {
            customerOrgId = org, code = Code(), name = "Route store", address = "1 Harbor St", defaultWarehouseId = warehouse,
        }));
        return (warehouse, stores.ToArray());
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/" + path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }
    private static string Code() => "R" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}
