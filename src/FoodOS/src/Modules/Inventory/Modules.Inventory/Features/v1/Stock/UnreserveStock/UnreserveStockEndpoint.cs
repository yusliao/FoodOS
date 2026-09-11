using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.UnreserveStock;

public static class UnreserveStockEndpoint
{
    internal static RouteHandlerBuilder MapUnreserveStockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stock/unreserve",
                async (UnreserveStockCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("UnreserveStock")
            .WithSummary("Release a SKU-level reservation before cutoff")
            .RequirePermission(InventoryPermissions.Stock.Reserve)
            .WithIdempotency();
    }
}
