using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.CreatePutawayTask;

public static class CreatePutawayTaskEndpoint
{
    internal static RouteHandlerBuilder MapCreatePutawayTaskEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/putaway-tasks",
                async (CreatePutawayTaskCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreatePutawayTask")
            .WithSummary("Create a putaway task for a received or returned lot")
            .RequirePermission(WarehousePermissions.Putaway.Create)
            .WithIdempotency();
    }
}
