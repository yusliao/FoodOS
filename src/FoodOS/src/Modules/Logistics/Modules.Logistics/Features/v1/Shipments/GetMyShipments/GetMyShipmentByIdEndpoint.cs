using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;

public static class GetMyShipmentByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetMyShipmentByIdEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/shipments/mine/{shipmentId:guid}",
                (Guid shipmentId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetMyShipmentByIdQuery(shipmentId), ct))
            .WithName("GetMyShipmentById")
            .WithSummary("Get a shipment assigned to the current driver")
            .RequirePermission(LogisticsPermissions.Shipments.ViewAssigned);
}
