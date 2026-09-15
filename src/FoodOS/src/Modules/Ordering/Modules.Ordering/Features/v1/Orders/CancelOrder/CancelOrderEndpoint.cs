using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;

public static class CancelOrderEndpoint
{
    internal static RouteHandlerBuilder MapCancelOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/orders/{orderId:guid}/cancel",
                async (Guid orderId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CancelOrderCommand(orderId), ct).ConfigureAwait(false)))
            .WithName("CancelOrder")
            .WithSummary("Cancel a reserved order before cutoff")
            .RequirePermission(OrderingPermissions.Orders.Manage)
            .WithIdempotency();
    }
}
