using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReceiveInventory;

public static class ReceiveInventoryEndpoint
{
    internal static RouteHandlerBuilder MapReceiveInventoryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stock/receive",
                async (ReceiveInventoryCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("ReceiveInventory")
            .WithSummary("Receive quantity onto a lot in a warehouse zone")
            .RequirePermission(InventoryPermissions.Stock.Receive)
            .WithIdempotency();
    }
}
