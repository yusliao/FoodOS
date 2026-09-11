using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Orders.GetOrderById;

public static class GetOrderByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetOrderByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/orders/{orderId:guid}",
                (Guid orderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetOrderByIdQuery(orderId), ct))
            .WithName("GetOrderById")
            .WithSummary("Get a sales order by id")
            .RequirePermission(OrderingPermissions.Shop.View);
    }
}
