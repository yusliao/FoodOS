using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;

public static class ConfirmLoadShipmentEndpoint
{
    internal static RouteHandlerBuilder MapConfirmLoadShipmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/shipments/{shipmentId:guid}/load",
                async (Guid shipmentId, LoadShipmentCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { ShipmentId = shipmentId }, ct).ConfigureAwait(false));
                })
            .WithName("LoadShipment")
            .WithSummary("Scan packed orders or totes onto the truck")
            .RequirePermission(LogisticsPermissions.Shipments.Load)
            .WithIdempotency();
    }
}
