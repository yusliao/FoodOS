using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.AdjustCountStock;

public static class AdjustCountStockEndpoint
{
    internal static RouteHandlerBuilder MapAdjustCountStockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stock/count",
                async (AdjustCountStockCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("AdjustCountStock")
            .WithSummary("Cycle-count a lot against available ATP")
            .RequirePermission(InventoryPermissions.Stock.Adjust)
            .WithIdempotency();
    }
}
