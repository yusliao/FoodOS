using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetShipmentById;

public static class GetShipmentByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetShipmentByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/shipments/{shipmentId:guid}",
                (Guid shipmentId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetShipmentByIdQuery(shipmentId), ct))
            .WithName("GetShipmentById")
            .WithSummary("Get a shipment")
            .RequirePermission(LogisticsPermissions.Shipments.View);
    }
}
