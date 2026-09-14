using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff.ConfirmCutoff;

public static class ConfirmCutoffEndpoint
{
    internal static RouteHandlerBuilder MapConfirmCutoffEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/warehouses/{warehouseId:guid}/cutoff",
                async (Guid warehouseId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ConfirmCutoffCommand(warehouseId), ct).ConfigureAwait(false)))
            .WithName("ConfirmCutoff")
            .WithSummary("Lock reserved orders, open the daily plan, and generate draft waves (release remains manual)")
            .RequirePermission(WarehousePermissions.Waves.Cutoff)
            .WithIdempotency();
    }
}
