using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SearchPurchaseOrders;

public static class SearchPurchaseOrdersEndpoint
{
    internal static RouteHandlerBuilder MapSearchPurchaseOrdersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/purchase-orders",
                (string? search, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchPurchaseOrdersQuery(search), ct))
            .WithName("SearchPurchaseOrders")
            .WithSummary("Search purchase orders")
            .RequirePermission(ProcurementPermissions.Purchase.View);
    }
}
