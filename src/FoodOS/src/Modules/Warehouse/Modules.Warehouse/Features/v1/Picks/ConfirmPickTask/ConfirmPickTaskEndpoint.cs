using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Picks.ConfirmPickTask;

public static class ConfirmPickTaskEndpoint
{
    internal static RouteHandlerBuilder MapConfirmPickTaskEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/pick-tasks/{pickTaskId:guid}/confirm",
                async (Guid pickTaskId, ConfirmPickTaskCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { PickTaskId = pickTaskId }, ct).ConfigureAwait(false));
                })
            .WithName("ConfirmPickTask")
            .WithSummary("Confirm a PDA pick by scanning the allocated lot")
            .RequirePermission(WarehousePermissions.Picks.Confirm)
            .WithIdempotency();
    }
}
