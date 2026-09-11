using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GenerateWave;

public static class GenerateWaveEndpoint
{
    internal static RouteHandlerBuilder MapGenerateWaveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/waves",
                async (GenerateWaveCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("GenerateWave")
            .WithSummary("Generate draft waves by zone from the cutoff daily plan")
            .RequirePermission(WarehousePermissions.Waves.Generate)
            .WithIdempotency();
    }
}
