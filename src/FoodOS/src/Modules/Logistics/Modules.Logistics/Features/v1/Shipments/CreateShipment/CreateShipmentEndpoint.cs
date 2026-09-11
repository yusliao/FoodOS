using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Shipments.CreateShipment;

public static class CreateShipmentEndpoint
{
    internal static RouteHandlerBuilder MapCreateShipmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/shipments",
                async (CreateShipmentCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateShipment")
            .WithSummary("Build a shipment from packed orders on a fixed route")
            .RequirePermission(LogisticsPermissions.Shipments.Create)
            .WithIdempotency();
    }
}
