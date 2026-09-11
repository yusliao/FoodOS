using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GetWaveById;

public static class GetWaveByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetWaveByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/waves/{waveId:guid}",
                (Guid waveId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetWaveByIdQuery(waveId), ct))
            .WithName("GetWaveById")
            .WithSummary("Get a wave and its pick tasks")
            .RequirePermission(WarehousePermissions.Waves.View);
    }
}
