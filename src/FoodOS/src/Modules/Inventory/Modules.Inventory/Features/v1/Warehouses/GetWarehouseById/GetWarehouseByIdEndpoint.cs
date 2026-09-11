using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.GetWarehouseById;

public static class GetWarehouseByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetWarehouseByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/warehouses/{warehouseId:guid}",
                (Guid warehouseId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetWarehouseByIdQuery(warehouseId), ct))
            .WithName("GetWarehouseById")
            .WithSummary("Get a warehouse by id")
            .RequirePermission(InventoryPermissions.Warehouses.View);
    }
}
