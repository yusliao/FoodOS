using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.SearchPutawayTasks;

public static class SearchPutawayTasksEndpoint
{
    internal static RouteHandlerBuilder MapSearchPutawayTasksEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/putaway-tasks",
                (Guid warehouseId, string? status, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchPutawayTasksQuery(warehouseId, status), ct))
            .WithName("SearchPutawayTasks")
            .WithSummary("List putaway tasks for a warehouse")
            .RequirePermission(WarehousePermissions.Putaway.View);
    }
}
