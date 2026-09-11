using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.v1.Locations;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Warehouse.Features.v1.Locations.CreateLocation;

public static class CreateLocationEndpoint
{
    internal static RouteHandlerBuilder MapCreateLocationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/locations",
                async (CreateLocationCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateLocation")
            .WithSummary("Create a warehouse location")
            .RequirePermission(WarehousePermissions.Locations.Create)
            .WithIdempotency();
    }
}
