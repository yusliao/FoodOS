using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.SearchLotBalances;

public static class SearchLotBalancesEndpoint
{
    internal static RouteHandlerBuilder MapSearchLotBalancesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stock/balances",
                (Guid warehouseId, TemperatureZoneKind? zone, Guid? productId, Guid? lotId, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(
                        new SearchLotBalancesQuery(
                            warehouseId,
                            zone,
                            productId,
                            lotId,
                            pageNumber is > 0 ? pageNumber.Value : 1,
                            pageSize is > 0 ? pageSize.Value : 20),
                        ct))
            .WithName("SearchLotBalances")
            .WithSummary("Lot balances at a warehouse")
            .RequirePermission(InventoryPermissions.Stock.View);
    }
}
