using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.AmendOrder;

public static class AmendOrderEndpoint
{
    internal static RouteHandlerBuilder MapAmendOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/orders/{orderId:guid}/amend",
                async (Guid orderId, AmendOrderCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command with { OrderId = orderId }, ct).ConfigureAwait(false)))
            .WithName("AmendOrder")
            .WithSummary("Amend a reserved order before cutoff")
            .RequirePermission(OrderingPermissions.Orders.Manage)
            .WithIdempotency();
    }
}
