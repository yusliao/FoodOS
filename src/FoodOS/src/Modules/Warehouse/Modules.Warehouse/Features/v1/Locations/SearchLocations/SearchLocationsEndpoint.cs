using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Locations;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Locations.SearchLocations;

public static class SearchLocationsEndpoint
{
    internal static RouteHandlerBuilder MapSearchLocationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/locations",
                (Guid warehouseId, Guid? zoneId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchLocationsQuery(warehouseId, zoneId), ct))
            .WithName("SearchLocations")
            .WithSummary("List warehouse locations")
            .RequirePermission(WarehousePermissions.Locations.View);
    }
}
