using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

[Collection(FshCollectionDefinition.Name)]
public sealed class OrderingAfterSalesTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OrderingAfterSalesTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task CreateAfterSales_Should_Conflict_When_OrderNotReceived()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouseId = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouseId);
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var order = SalesOrder.CreateDraft(
            $"AS{Guid.NewGuid():N}"[..32], storeId, orgId, warehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow.AddHours(1),
            [(productId, "Ambient", 2m, 9.5m, "USD")]);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        using var claim = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/after-sales",
            new
            {
                orderId = order.Id,
                orderLineId = order.Lines[0].Id,
                type = "Return",
                quantity = 1m,
                reason = "bruised",
            });
        claim.StatusCode.ShouldBe(HttpStatusCode.Conflict, await claim.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CreateWarehouseAsync(HttpClient client)
    {
        var code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        return await create.DeserializeAsync<Guid>();
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

        var sku = $"AS-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name = Unique("Cod"),
                description = "Shop SKU",
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

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
