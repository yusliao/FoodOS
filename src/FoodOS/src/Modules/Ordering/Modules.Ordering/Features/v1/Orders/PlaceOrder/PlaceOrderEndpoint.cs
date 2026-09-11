using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;

public static class PlaceOrderEndpoint
{
    internal static RouteHandlerBuilder MapPlaceOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/orders",
                async (PlaceOrderCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("PlaceOrder")
            .WithSummary("Place an order from the store cart and reserve ATP")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
    }
}
