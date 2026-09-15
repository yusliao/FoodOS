using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Carts.GetCart;

public static class GetCartEndpoint
{
    internal static RouteHandlerBuilder MapGetCartEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/carts/{storeId:guid}",
                (Guid storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetCartQuery(storeId), ct))
            .WithName("GetCart")
            .WithSummary("Get the cart for a store")
            .RequirePermission(OrderingPermissions.Orders.View);
    }
}
