using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Lots;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Lots.SearchLots;

public static class SearchLotsEndpoint
{
    internal static RouteHandlerBuilder MapSearchLotsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/lots",
                (Guid? warehouseId, Guid? productId, string? lotNo, string? status, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(
                        new SearchLotsQuery(
                            warehouseId,
                            productId,
                            lotNo,
                            status,
                            pageNumber is > 0 ? pageNumber.Value : 1,
                            pageSize is > 0 ? pageSize.Value : 20),
                        ct))
            .WithName("SearchLots")
            .WithSummary("Search lot masters")
            .RequirePermission(InventoryPermissions.Stock.View);
    }
}
