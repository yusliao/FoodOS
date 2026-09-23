using FSH.Modules.Ordering.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    private static async Task AssertCartPreservedAsync(
        HttpClient client, Guid storeId, Guid productId, decimal quantity)
    {
        using var response = await client.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeId}/cart");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var line = (await response.DeserializeAsync<ShopCartDto>()).Lines.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(productId);
        line.Quantity.ShouldBe(quantity);
    }
}
