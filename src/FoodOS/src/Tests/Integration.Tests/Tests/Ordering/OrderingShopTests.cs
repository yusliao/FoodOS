using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

/// <summary>
/// Shop path: org/store → cart → place (ReserveStock) → amend/cancel before cutoff.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class OrderingShopTests
{
    private readonly AuthHelper _auth;

    public OrderingShopTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task PlaceAmendCancel_Should_ReserveAndReleaseAtp()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-SO1", 10m);

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 6m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var getPlaced = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var placed = await getPlaced.DeserializeAsync<SalesOrderDto>();
        placed.Status.ShouldBe("Reserved");
        placed.Lines.Count.ShouldBe(1);
        placed.Lines[0].OrderedQty.ShouldBe(6m);
        placed.Lines[0].ReservationId.ShouldNotBeNull();

        var afterPlace = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterPlace.Available.ShouldBe(4m);

        using var getCart = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        var cart = await getCart.DeserializeAsync<CartDto>();
        cart.Lines.ShouldBeEmpty();

        using var amend = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId, quantity = 3m } } });
        amend.StatusCode.ShouldBe(HttpStatusCode.OK, await amend.Content.ReadAsStringAsync());

        using var getAmended = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var amended = await getAmended.DeserializeAsync<SalesOrderDto>();
        amended.Status.ShouldBe("Reserved");
        amended.Revision.ShouldBe(1);
        amended.Lines.ShouldHaveSingleItem().OrderedQty.ShouldBe(3m);

        var afterAmend = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterAmend.Available.ShouldBe(7m);

        using var cancel = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/cancel",
            new { });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        using var getCancelled = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var cancelled = await getCancelled.DeserializeAsync<SalesOrderDto>();
        cancelled.Status.ShouldBe("Cancelled");

        var afterCancel = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterCancel.Available.ShouldBe(10m);
    }

    [Fact]
    public async Task PlaceOrder_Should_Conflict_When_InsufficientAtp()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-SO2", 2m);

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 5m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.Conflict, await place.Content.ReadAsStringAsync());

        var available = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        available.Available.ShouldBe(2m);
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

        var sku = $"SO-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
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

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
