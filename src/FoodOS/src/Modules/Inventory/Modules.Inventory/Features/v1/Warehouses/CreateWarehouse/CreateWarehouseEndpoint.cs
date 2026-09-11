using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.CreateWarehouse;

public static class CreateWarehouseEndpoint
{
    internal static RouteHandlerBuilder MapCreateWarehouseEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/warehouses",
                async (CreateWarehouseCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateWarehouse")
            .WithSummary("Create a warehouse with three temperature zones")
            .RequirePermission(InventoryPermissions.Warehouses.Create)
            .WithIdempotency();
    }
}
