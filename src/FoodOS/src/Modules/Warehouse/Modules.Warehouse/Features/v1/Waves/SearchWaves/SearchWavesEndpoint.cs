using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Waves.SearchWaves;

public static class SearchWavesEndpoint
{
    internal static RouteHandlerBuilder MapSearchWavesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/waves",
                (Guid warehouseId, DateOnly? businessDate, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchWavesQuery(warehouseId, businessDate), ct))
            .WithName("SearchWaves")
            .WithSummary("List waves for a warehouse")
            .RequirePermission(WarehousePermissions.Waves.View);
    }
}
