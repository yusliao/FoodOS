using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopAfterSales;

public static class ShopAfterSalesEndpoints
{
    internal static void MapShopAfterSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSearchShopAfterSalesEndpoint();
        endpoints.MapCreateShopAfterSalesEndpoint();
    }
}

public static class SearchShopAfterSalesEndpoint
{
    internal static RouteHandlerBuilder MapSearchShopAfterSalesEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/after-sales",
                (Guid? storeId, Guid? orderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchShopAfterSalesQuery(storeId, orderId), ct))
            .WithName("SearchShopAfterSales")
            .RequirePermission(OrderingPermissions.Shop.View);
}

public static class CreateShopAfterSalesEndpoint
{
    internal static RouteHandlerBuilder MapCreateShopAfterSalesEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/after-sales",
                (CreateShopAfterSalesCommand command, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(command, ct))
            .WithName("CreateShopAfterSales")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
}
