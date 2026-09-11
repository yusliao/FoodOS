using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SendPurchaseOrder;

public static class SendPurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapSendPurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{purchaseOrderId:guid}/send",
                async (Guid purchaseOrderId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SendPurchaseOrderCommand(purchaseOrderId), ct).ConfigureAwait(false)))
            .WithName("SendPurchaseOrder")
            .WithSummary("Mark a draft purchase order as sent")
            .RequirePermission(ProcurementPermissions.Purchase.Create)
            .WithIdempotency();
    }
}
