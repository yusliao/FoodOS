using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Picks.GetMyPickTasks;

public static class GetMyPickTasksEndpoint
{
    internal static RouteHandlerBuilder MapGetMyPickTasksEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/pick-tasks/mine",
                (Guid? warehouseId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetMyPickTasksQuery(warehouseId), ct))
            .WithName("GetMyPickTasks")
            .WithSummary("List open pick tasks for PDA")
            .RequirePermission(WarehousePermissions.Picks.View);
    }
}
