using System.Globalization;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Catalog;

/// <summary>
/// Playbook E: customer A must not see customer B's contract price via quote or list APIs.
/// Ordering snapshots the quote; catalog <c>Product.Price</c> stays the list price.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class PriceListTests
{
    private readonly AuthHelper _auth;

    public PriceListTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Quote_Should_ResolveLockThenContractThenCatalog_And_IsolateCustomers()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var productId = await CreateProductAsync(client, listPrice: 20m);
        var orgA = await CreateCustomerOrgAsync(client);
        var orgB = await CreateCustomerOrgAsync(client);

        await CreatePriceListAsync(client, orgA, productId, 8m);
        await CreatePriceListAsync(client, orgB, productId, 12m);

        var quoteA = await QuoteAsync(client, orgA, productId, 5m);
        quoteA.UnitPrice.ShouldBe(8m);
        quoteA.Source.ShouldBe("Contract");
        quoteA.Currency.ShouldBe("USD");

        var quoteB = await QuoteAsync(client, orgB, productId, 5m);
        quoteB.UnitPrice.ShouldBe(12m);
        quoteB.Source.ShouldBe("Contract");

        using var listA = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/price-lists?customerOrgId={orgA}");
        var listsA = await listA.DeserializeAsync<List<PriceListDto>>();
        listsA.ShouldNotBeEmpty();
        listsA.ShouldAllBe(l => l.CustomerOrgId == orgA);
        listsA.SelectMany(l => l.Lines).ShouldNotContain(l => l.UnitPrice == 12m);

        using var productResponse = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/products/{productId}");
        var product = await productResponse.DeserializeAsync<ProductDto>();
        product.Price.Amount.ShouldBe(20m);

        using var lockResponse = await client.PutAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/price-locks",
            new
            {
                customerOrgId = orgA,
                productId,
                unitPrice = 6m,
                currency = "USD",
                until = DateTimeOffset.UtcNow.AddDays(7),
            });
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());

        var lockedA = await QuoteAsync(client, orgA, productId, 5m);
        lockedA.UnitPrice.ShouldBe(6m);
        lockedA.Source.ShouldBe("Locked");

        var stillB = await QuoteAsync(client, orgB, productId, 5m);
        stillB.UnitPrice.ShouldBe(12m);
        stillB.Source.ShouldBe("Contract");
    }

    [Fact]
    public async Task PlaceOrder_Should_FailClosed_WhileContractQuoteAndCartRemainAvailable()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouseId = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client, listPrice: 20m);
        var orgId = await CreateCustomerOrgAsync(client);
        await CreatePriceListAsync(client, orgId, productId, 8m, minQty: 1m);
        var storeId = await CreateStoreAsync(client, orgId, warehouseId);
        var quote = await QuoteAsync(client, orgId, productId, 4m);
        quote.UnitPrice.ShouldBe(8m);
        quote.Currency.ShouldBe("USD");
        quote.Source.ShouldBe("Contract");

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 4m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.Conflict, await place.Content.ReadAsStringAsync());
        (await place.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");

        using var getCart = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        getCart.StatusCode.ShouldBe(HttpStatusCode.OK, await getCart.Content.ReadAsStringAsync());
        var line = (await getCart.DeserializeAsync<CartDto>()).Lines.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(productId);
        line.Quantity.ShouldBe(4m);
    }

    private static async Task<PriceQuoteDto> QuoteAsync(
        HttpClient client,
        Guid customerOrgId,
        Guid productId,
        decimal quantity)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.CatalogBasePath}/quotes?customerOrgId={customerOrgId}&productId={productId}&quantity={quantity.ToString(CultureInfo.InvariantCulture)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<PriceQuoteDto>();
    }

    private static async Task CreatePriceListAsync(
        HttpClient client,
        Guid customerOrgId,
        Guid productId,
        decimal unitPrice,
        decimal minQty = 1m)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/price-lists",
            new
            {
                name = Unique("List"),
                customerOrgId,
                validFrom = DateTimeOffset.UtcNow.AddDays(-1),
                validTo = DateTimeOffset.UtcNow.AddYears(1),
                priority = 1,
                lines = new[]
                {
                    new { productId, minQty, unitPrice, currency = "USD" }
                }
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, decimal listPrice)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brandResp.StatusCode.ShouldBe(HttpStatusCode.OK, await brandResp.Content.ReadAsStringAsync());

        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Cat"), description = (string?)null, parentCategoryId = (Guid?)null });
        categoryResp.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResp.Content.ReadAsStringAsync());

        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku = $"PL-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Sku"),
                description = "Priced SKU",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = listPrice,
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

    private static async Task<Guid> CreateWarehouseAsync(HttpClient client)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new
            {
                code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("WH"),
                city = "Boston",
                timeZoneId = (string?)null
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        return await create.DeserializeAsync<Guid>();
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
