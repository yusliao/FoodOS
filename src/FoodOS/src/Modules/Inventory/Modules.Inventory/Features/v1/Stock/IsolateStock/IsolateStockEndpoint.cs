using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.IsolateStock;

public static class IsolateStockEndpoint
{
    internal static RouteHandlerBuilder MapIsolateStockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stock/isolate",
                async (IsolateStockCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("IsolateStock")
            .WithSummary("Freeze available quantity on a lot; ATP decreases, OnHand does not increase elsewhere")
            .RequirePermission(InventoryPermissions.Stock.Isolate)
            .WithIdempotency();
    }
}
