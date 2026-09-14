using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Vehicles;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Vehicles.SearchVehicles;

public static class SearchVehiclesEndpoint
{
    internal static RouteHandlerBuilder MapSearchVehiclesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/vehicles",
                (IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchVehiclesQuery(), ct))
            .WithName("SearchVehicles")
            .WithSummary("List delivery vehicles")
            .RequirePermission(LogisticsPermissions.Vehicles.View);
    }
}
