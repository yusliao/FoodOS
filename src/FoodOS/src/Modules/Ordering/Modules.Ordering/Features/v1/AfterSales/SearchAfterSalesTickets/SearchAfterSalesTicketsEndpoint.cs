using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.SearchAfterSalesTickets;

public static class SearchAfterSalesTicketsEndpoint
{
    internal static RouteHandlerBuilder MapSearchAfterSalesTicketsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/after-sales",
                (Guid storeId, Guid? orderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchAfterSalesTicketsQuery(storeId, orderId), ct))
            .WithName("SearchAfterSalesTickets")
            .WithSummary("List after-sales tickets for a store")
            .RequirePermission(OrderingPermissions.Shop.View);
    }
}
