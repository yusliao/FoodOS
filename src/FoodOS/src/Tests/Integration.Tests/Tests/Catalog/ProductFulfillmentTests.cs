using FSH.Modules.Catalog.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Catalog;

/// <summary>
/// HTTP coverage for foodservice fulfillment attributes and localized product copy.
/// Domain rules live in <c>Catalog.Tests</c>; this file asserts the API + Accept-Language path.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class ProductFulfillmentTests
{
    private readonly AuthHelper _auth;

    public ProductFulfillmentTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task CreateProduct_Should_DefaultToAmbientEaAndUsd()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var (brandId, categoryId) = await CreateBrandAndCategoryAsync(client);
        var productId = await CreateProductAsync(client, brandId, categoryId, "Canonical salmon");

        using var response = await client.GetAsync($"{TestConstants.CatalogBasePath}/products/{productId}");
        var product = await response.DeserializeAsync<ProductDto>();

        product.Price.Currency.ShouldBe("USD");
        product.TemperatureZone.ShouldBe("Ambient");
        product.BaseUom.ShouldBe("EA");
        product.CatchWeight.ShouldBeFalse();
        product.MinRemainingDaysOnShip.ShouldBe(0);
        product.Translations.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateFulfillmentAndTranslation_Should_LocalizeName_When_AcceptLanguageMatches()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var (brandId, categoryId) = await CreateBrandAndCategoryAsync(client);
        var productId = await CreateProductAsync(client, brandId, categoryId, "Canonical salmon");

        using var fulfillmentResponse = await client.PutAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products/{productId}/fulfillment",
            new
            {
                productId,
                temperatureZone = "Frozen",
                shelfLifeDays = 180,
                minRemainingDaysOnShip = 14,
                baseUom = "lb",
                catchWeight = true,
                barcode = "012345678905",
                storageNote = "Keep frozen",
            });
        fulfillmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await fulfillmentResponse.Content.ReadAsStringAsync());

        using var translationResponse = await client.PutAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products/{productId}/translations/zh-CN",
            new { name = "冷冻三文鱼", description = "保持冷冻" });
        translationResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await translationResponse.Content.ReadAsStringAsync());

        using var zhRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{TestConstants.CatalogBasePath}/products/{productId}");
        zhRequest.Headers.AcceptLanguage.ParseAdd("zh-CN");
        using var zhResponse = await client.SendAsync(zhRequest);
        var zhProduct = await zhResponse.DeserializeAsync<ProductDto>();

        zhProduct.Name.ShouldBe("冷冻三文鱼");
        zhProduct.Description.ShouldBe("保持冷冻");
        zhProduct.TemperatureZone.ShouldBe("Frozen");
        zhProduct.BaseUom.ShouldBe("LB");
        zhProduct.CatchWeight.ShouldBeTrue();
        zhProduct.ShelfLifeDays.ShouldBe(180);
        zhProduct.MinRemainingDaysOnShip.ShouldBe(14);
        zhProduct.Translations.ShouldContain(t => t.Culture == "zh-CN" && t.Name == "冷冻三文鱼");

        using var enRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{TestConstants.CatalogBasePath}/products/{productId}");
        enRequest.Headers.AcceptLanguage.ParseAdd("en-US");
        using var enResponse = await client.SendAsync(enRequest);
        var enProduct = await enResponse.DeserializeAsync<ProductDto>();
        enProduct.Name.ShouldBe("Canonical salmon");
    }

    private static async Task<(Guid BrandId, Guid CategoryId)> CreateBrandAndCategoryAsync(HttpClient client)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = UniqueName("Brand"), description = (string?)null, logoUrl = (string?)null });
        brandResp.StatusCode.ShouldBe(HttpStatusCode.OK, await brandResp.Content.ReadAsStringAsync());

        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = UniqueName("Cat"), description = (string?)null, parentCategoryId = (Guid?)null });
        categoryResp.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResp.Content.ReadAsStringAsync());

        return (await brandResp.DeserializeAsync<Guid>(), await categoryResp.DeserializeAsync<Guid>());
    }

    private static async Task<Guid> CreateProductAsync(
        HttpClient client,
        Guid brandId,
        Guid categoryId,
        string name)
    {
        var sku = $"TST-FF-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name,
                description = "Canonical description",
                brandId,
                categoryId,
                priceAmount = 12.5m,
                priceCurrency = "USD",
                stock = 0,
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static string UniqueName(string prefix) =>
        $"Fulfill-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
