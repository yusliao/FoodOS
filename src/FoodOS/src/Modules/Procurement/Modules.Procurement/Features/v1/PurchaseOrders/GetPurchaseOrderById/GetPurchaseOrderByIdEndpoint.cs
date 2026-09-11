using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrderById;

public static class GetPurchaseOrderByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetPurchaseOrderByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/purchase-orders/{purchaseOrderId:guid}",
                (Guid purchaseOrderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetPurchaseOrderByIdQuery(purchaseOrderId), ct))
            .WithName("GetPurchaseOrderById")
            .WithSummary("Get a purchase order by id")
            .RequirePermission(ProcurementPermissions.Purchase.View);
    }
}
