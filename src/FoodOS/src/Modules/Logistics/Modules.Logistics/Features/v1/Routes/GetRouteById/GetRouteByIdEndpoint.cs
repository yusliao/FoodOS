using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Routes.GetRouteById;

public static class GetRouteByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetRouteByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/routes/{routeId:guid}",
                (Guid routeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetRouteByIdQuery(routeId), ct))
            .WithName("GetRouteById")
            .WithSummary("Get a fixed route")
            .RequirePermission(LogisticsPermissions.Routes.View);
    }
}
