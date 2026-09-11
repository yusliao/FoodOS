using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.DepartShipment;

public static class ConfirmDepartShipmentEndpoint
{
    internal static RouteHandlerBuilder MapConfirmDepartShipmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/shipments/{shipmentId:guid}/depart",
                async (Guid shipmentId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new DepartShipmentCommand(shipmentId), ct).ConfigureAwait(false)))
            .WithName("DepartShipment")
            .WithSummary("Depart a loaded shipment and move orders in transit")
            .RequirePermission(LogisticsPermissions.Shipments.Depart)
            .WithIdempotency();
    }
}
