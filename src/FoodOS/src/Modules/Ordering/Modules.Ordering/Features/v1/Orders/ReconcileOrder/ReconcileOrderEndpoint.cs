using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.ReconcileOrder;

public static class ConfirmReconcileOrderEndpoint
{
    internal static RouteHandlerBuilder MapConfirmReconcileOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/orders/{orderId:guid}/reconcile",
                async (Guid orderId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReconcileOrderCommand(orderId), ct).ConfigureAwait(false)))
            .WithName("ReconcileOrder")
            .WithSummary("Close a received order after operational reconcile")
            .RequirePermission(OrderingPermissions.Orders.Reconcile)
            .WithIdempotency();
    }
}
