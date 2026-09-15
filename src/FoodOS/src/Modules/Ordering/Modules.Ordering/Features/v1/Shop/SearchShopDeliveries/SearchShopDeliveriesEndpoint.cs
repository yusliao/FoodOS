using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopDeliveries;

public static class SearchShopDeliveriesEndpoint
{
    internal static RouteHandlerBuilder MapSearchShopDeliveriesEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/deliveries",
                (Guid? storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchShopDeliveriesQuery(storeId), ct))
            .WithName("SearchShopDeliveries")
            .WithSummary("List delivery progress for authorized customer stores")
            .RequirePermission(OrderingPermissions.Shop.View);
}
