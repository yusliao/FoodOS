using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopProducts;

public static class SearchShopProductsEndpoint
{
    internal static RouteHandlerBuilder MapSearchShopProductsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/products",
                (Guid? storeId, string? search, Guid? brandId, Guid? categoryId,
                 int pageNumber, int pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchShopProductsQuery(
                        storeId,
                        search,
                        brandId,
                        categoryId,
                        pageNumber == 0 ? 1 : pageNumber,
                        pageSize == 0 ? 20 : pageSize), ct))
            .WithName("SearchShopProducts")
            .WithSummary("Search products available to the current restaurant at its applicable price")
            .RequirePermission(OrderingPermissions.Shop.View);
    }
}
