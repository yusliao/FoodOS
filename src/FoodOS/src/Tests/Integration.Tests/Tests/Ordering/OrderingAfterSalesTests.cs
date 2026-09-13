using FSH.Modules.Ordering.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

[Collection(FshCollectionDefinition.Name)]
public sealed class OrderingAfterSalesTests
{
    private readonly AuthHelper _auth;

    public OrderingAfterSalesTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task CreateAfterSales_Should_Conflict_When_OrderNotReceived()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouseId = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouseId, productId, "LOT-AS1", 8m);

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouseId);

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 2m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var get = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var order = await get.DeserializeAsync<SalesOrderDto>();

        using var claim = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/after-sales",
            new
            {
                orderId,
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

    private static async Task ReceiveAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string lotNo,
        decimal quantity)
    {
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
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
