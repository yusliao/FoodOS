using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Routes.SearchRoutes;

public static class SearchRoutesEndpoint
{
    internal static RouteHandlerBuilder MapSearchRoutesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/routes",
                (Guid warehouseId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchRoutesQuery(warehouseId), ct))
            .WithName("SearchRoutes")
            .WithSummary("List fixed routes for a warehouse")
            .RequirePermission(LogisticsPermissions.Routes.View);
    }
}
