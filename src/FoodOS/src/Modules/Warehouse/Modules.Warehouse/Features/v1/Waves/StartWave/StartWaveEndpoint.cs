using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Waves.StartWave;

public static class StartWaveEndpoint
{
    internal static RouteHandlerBuilder MapStartWaveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/waves/{waveId:guid}/release",
                async (Guid waveId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new StartWaveCommand(waveId), ct).ConfigureAwait(false)))
            .WithName("StartWave")
            .WithSummary("FEFO-allocate lots onto pick tasks and release the wave")
            .RequirePermission(WarehousePermissions.Waves.Release)
            .WithIdempotency();
    }
}
