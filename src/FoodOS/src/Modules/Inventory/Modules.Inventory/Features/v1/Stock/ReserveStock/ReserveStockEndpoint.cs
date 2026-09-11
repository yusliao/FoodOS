using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReserveStock;

public static class ReserveStockEndpoint
{
    internal static RouteHandlerBuilder MapReserveStockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stock/reserve",
                async (ReserveStockCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("ReserveStock")
            .WithSummary("Hold ATP at warehouse × zone × SKU without locking a lot")
            .RequirePermission(InventoryPermissions.Stock.Reserve)
            .WithIdempotency();
    }
}
