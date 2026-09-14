using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Drivers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Drivers.SearchDrivers;

public static class SearchDriversEndpoint
{
    internal static RouteHandlerBuilder MapSearchDriversEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/drivers",
                (IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchDriversQuery(), ct))
            .WithName("SearchDrivers")
            .WithSummary("List drivers")
            .RequirePermission(LogisticsPermissions.Drivers.View);
    }
}
