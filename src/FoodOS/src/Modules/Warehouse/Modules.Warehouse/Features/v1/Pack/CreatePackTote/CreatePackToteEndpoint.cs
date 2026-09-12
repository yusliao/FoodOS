using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Pack;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Pack.CreatePackTote;

public static class CreatePackToteEndpoint
{
    internal static RouteHandlerBuilder MapCreatePackToteEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/waves/{waveId:guid}/pack",
                async (Guid waveId, CreatePackToteCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(body with { WaveId = waveId }, ct).ConfigureAwait(false));
                })
            .WithName("CreatePackTote")
            .WithSummary("Pack completed wave orders onto a tote (SSCC)")
            .RequirePermission(WarehousePermissions.Pack.Create)
            .WithIdempotency();
    }
}
