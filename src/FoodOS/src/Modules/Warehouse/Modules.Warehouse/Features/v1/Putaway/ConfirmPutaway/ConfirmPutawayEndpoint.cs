using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.ConfirmPutaway;

public static class ConfirmPutawayEndpoint
{
    internal static RouteHandlerBuilder MapConfirmPutawayEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/putaway-tasks/{putawayTaskId:guid}/confirm",
                async (Guid putawayTaskId, ConfirmPutawayCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { PutawayTaskId = putawayTaskId }, ct).ConfigureAwait(false));
                })
            .WithName("ConfirmPutaway")
            .WithSummary("Confirm putaway onto a same-zone storage or pick location")
            .RequirePermission(WarehousePermissions.Putaway.Confirm)
            .WithIdempotency();
    }
}
