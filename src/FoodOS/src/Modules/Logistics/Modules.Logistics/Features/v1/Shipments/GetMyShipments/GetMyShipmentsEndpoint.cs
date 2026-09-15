using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;

public static class GetMyShipmentsEndpoint
{
    internal static RouteHandlerBuilder MapGetMyShipmentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/shipments/mine",
                (IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetMyShipmentsQuery(), ct))
            .WithName("GetMyShipments")
            .WithSummary("List shipments assigned to the current driver")
            .RequirePermission(LogisticsPermissions.Shipments.ViewAssigned);
    }
}
