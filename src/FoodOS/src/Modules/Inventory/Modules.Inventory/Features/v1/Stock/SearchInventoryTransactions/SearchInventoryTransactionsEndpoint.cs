using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Stock.SearchInventoryTransactions;

public static class SearchInventoryTransactionsEndpoint
{
    internal static RouteHandlerBuilder MapSearchInventoryTransactionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stock/transactions",
                (Guid warehouseId, Guid? lotId, Guid? productId, TemperatureZoneKind? zone, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(
                        new SearchInventoryTransactionsQuery(
                            warehouseId,
                            lotId,
                            productId,
                            zone,
                            pageNumber is > 0 ? pageNumber.Value : 1,
                            pageSize is > 0 ? pageSize.Value : 20),
                        ct))
            .WithName("SearchInventoryTransactions")
            .WithSummary("Immutable inventory ledger")
            .RequirePermission(InventoryPermissions.Stock.View);
    }
}
