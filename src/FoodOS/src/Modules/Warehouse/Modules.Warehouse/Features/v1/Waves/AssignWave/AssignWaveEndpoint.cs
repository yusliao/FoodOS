using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Waves.AssignWave;

public static class AssignWaveEndpoint
{
    internal static RouteHandlerBuilder MapAssignWaveEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/waves/{waveId:guid}/assign",
                async (Guid waveId, AssignWaveCommand body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(body with { WaveId = waveId }, ct).ConfigureAwait(false)))
            .WithName("AssignWave")
            .WithSummary("Assign a wave to an active operator picker")
            .RequirePermission(WarehousePermissions.Waves.Assign);
}
