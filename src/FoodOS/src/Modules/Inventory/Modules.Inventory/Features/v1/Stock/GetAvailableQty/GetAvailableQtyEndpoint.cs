using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.GetAvailableQty;

public static class GetAvailableQtyEndpoint
{
    internal static RouteHandlerBuilder MapGetAvailableQtyEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stock/available",
                (Guid warehouseId, Guid productId, TemperatureZoneKind? zone, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetAvailableQtyQuery(warehouseId, productId, zone), ct))
            .WithName("GetAvailableQty")
            .WithSummary("ATP quantity at warehouse × zone (lot-aware)")
            .RequirePermission(InventoryPermissions.Stock.View);
    }
}
