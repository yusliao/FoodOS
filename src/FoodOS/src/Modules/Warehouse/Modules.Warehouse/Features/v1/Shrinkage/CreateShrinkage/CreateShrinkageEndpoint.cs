using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Shrinkage;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Shrinkage.CreateShrinkage;

public static class CreateShrinkageEndpoint
{
    internal static RouteHandlerBuilder MapCreateShrinkageEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/shrinkage",
                async (CreateShrinkageCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateShrinkage")
            .WithSummary("Record lot shrinkage; posts AdjustShrink to inventory")
            .RequirePermission(WarehousePermissions.Shrinkage.Create)
            .WithIdempotency();
    }
}
