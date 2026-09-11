using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;

public static class SearchOrdersEndpoint
{
    internal static RouteHandlerBuilder MapSearchOrdersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/orders",
                (Guid? storeId, int pageNumber, int pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(
                        new SearchOrdersQuery(storeId, pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize),
                        ct))
            .WithName("SearchOrders")
            .WithSummary("Search sales orders")
            .RequirePermission(OrderingPermissions.Shop.View);
    }
}
