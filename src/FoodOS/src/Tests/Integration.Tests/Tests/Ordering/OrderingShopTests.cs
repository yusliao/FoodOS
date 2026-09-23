using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Data;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

/// <summary>
/// External-WMS mode keeps cart maintenance available but blocks place, amend and cancel before local orders or reservations are written.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class OrderingShopTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OrderingShopTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task BlockedPlacement_Should_PreserveCart_WithoutOrderOrReservation()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateCartSetupAsync(client, 6m);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId = setup.StoreId });
        await AssertBlockedAsync(place);

        await AssertCartAsync(client, setup.StoreId, setup.ProductId, 6m);
        await AssertNoOrderOrReservationAsync(setup.StoreId, setup.ProductId);
        (await GetAvailableAsync(client, setup.WarehouseId, setup.ProductId, "Ambient")).Available.ShouldBe(0m);
    }

    [Fact]
    public async Task PlaceAmendCancel_Should_AllFailClosed_WithoutLocalState()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateCartSetupAsync(client, 6m);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId = setup.StoreId });
        await AssertBlockedAsync(place);
        var orderId = Guid.NewGuid();
        using var amend = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId = setup.ProductId, quantity = 3m } } });
        await AssertBlockedAsync(amend);
        using var cancel = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/cancel", new { });
        await AssertBlockedAsync(cancel);

        await AssertCartAsync(client, setup.StoreId, setup.ProductId, 6m);
        await AssertNoOrderOrReservationAsync(setup.StoreId, setup.ProductId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelRetry_Should_RemainBlocked_BeforeAndAfterBlockedCutoff(bool afterCutoff)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateCartSetupAsync(client, 4m);
        if (afterCutoff)
        {
            using var cutoff = await client.PostAsJsonAsync(
                $"{TestConstants.WarehouseBasePath}/warehouses/{setup.WarehouseId}/cutoff", new { });
            await AssertBlockedAsync(cutoff);
        }

        var orderId = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var cancel = await client.PostAsJsonAsync(
                $"{TestConstants.OrderingBasePath}/orders/{orderId}/cancel", new { });
            await AssertBlockedAsync(cancel);
        }

        await AssertCartAsync(client, setup.StoreId, setup.ProductId, 4m);
        await AssertNoOrderOrReservationAsync(setup.StoreId, setup.ProductId);
    }

    [Fact]
    public async Task PlaceOrder_Should_ReportExternalWmsBoundary_InsteadOfLocalAtpDecision()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var setup = await CreateCartSetupAsync(client, 5m);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders", new { storeId = setup.StoreId });
        await AssertBlockedAsync(place);

        (await GetAvailableAsync(client, setup.WarehouseId, setup.ProductId, "Ambient")).Available.ShouldBe(0m);
        await AssertCartAsync(client, setup.StoreId, setup.ProductId, 5m);
        await AssertNoOrderOrReservationAsync(setup.StoreId, setup.ProductId);
    }

    private async Task AssertNoOrderOrReservationAsync(Guid storeId, Guid productId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await ordering.SalesOrders.AnyAsync(order => order.StoreId == storeId)).ShouldBeFalse();
        (await inventory.Reservations.AnyAsync(reservation => reservation.ProductId == productId)).ShouldBeFalse();
    }

    private static async Task AssertCartAsync(
        HttpClient client, Guid storeId, Guid productId, decimal quantity)
    {
        using var getCart = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        getCart.StatusCode.ShouldBe(HttpStatusCode.OK, await getCart.Content.ReadAsStringAsync());
        var line = (await getCart.DeserializeAsync<CartDto>()).Lines.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(productId);
        line.Quantity.ShouldBe(quantity);
    }

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private static async Task<CartSetup> CreateCartSetupAsync(HttpClient client, decimal quantity)
    {
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());
        return new CartSetup(warehouse.Id, storeId, productId);
    }

    private sealed record CartSetup(Guid WarehouseId, Guid StoreId, Guid ProductId);

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
        using var brand = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brand.StatusCode.ShouldBe(HttpStatusCode.OK, await brand.Content.ReadAsStringAsync());
        using var category = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Cat"), description = (string?)null, parentCategoryId = (Guid?)null });
        category.StatusCode.ShouldBe(HttpStatusCode.OK, await category.Content.ReadAsStringAsync());
        using var product = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku = $"SO-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Cod"),
                description = "Shop SKU",
                brandId = await brand.DeserializeAsync<Guid>(),
                categoryId = await category.DeserializeAsync<Guid>(),
                priceAmount = 9.5m,
                priceCurrency = "USD",
                stock = 0,
            });
        product.StatusCode.ShouldBe(HttpStatusCode.OK, await product.Content.ReadAsStringAsync());
        return await product.DeserializeAsync<Guid>();
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

    private static async Task<AvailableQtyDto> GetAvailableAsync(
        HttpClient client, Guid warehouseId, Guid productId, string zone)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone={zone}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<AvailableQtyDto>();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
