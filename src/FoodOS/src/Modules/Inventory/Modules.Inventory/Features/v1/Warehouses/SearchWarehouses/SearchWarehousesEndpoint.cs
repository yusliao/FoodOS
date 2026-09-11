using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.SearchWarehouses;

public static class SearchWarehousesEndpoint
{
    internal static RouteHandlerBuilder MapSearchWarehousesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/warehouses",
                (string? search, int pageNumber, int pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchWarehousesQuery(search, pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize), ct))
            .WithName("SearchWarehouses")
            .WithSummary("Search warehouses")
            .RequirePermission(InventoryPermissions.Warehouses.View);
    }
}
