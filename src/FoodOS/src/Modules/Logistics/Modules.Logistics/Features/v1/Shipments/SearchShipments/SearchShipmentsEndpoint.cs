using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.SearchShipments;

public static class SearchShipmentsEndpoint
{
    internal static RouteHandlerBuilder MapSearchShipmentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/shipments",
                (Guid warehouseId, DateOnly? businessDate, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchShipmentsQuery(warehouseId, businessDate), ct))
            .WithName("SearchShipments")
            .WithSummary("List shipments for a warehouse")
            .RequirePermission(LogisticsPermissions.Shipments.View);
    }
}
