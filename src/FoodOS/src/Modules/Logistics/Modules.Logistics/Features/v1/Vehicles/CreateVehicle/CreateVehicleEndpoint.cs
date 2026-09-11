using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Vehicles;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Vehicles.CreateVehicle;

public static class CreateVehicleEndpoint
{
    internal static RouteHandlerBuilder MapCreateVehicleEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/vehicles",
                async (CreateVehicleCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateVehicle")
            .WithSummary("Register a delivery vehicle")
            .RequirePermission(LogisticsPermissions.Vehicles.Create)
            .WithIdempotency();
    }
}
