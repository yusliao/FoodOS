using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Drivers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Drivers.CreateDriver;

public static class CreateDriverEndpoint
{
    internal static RouteHandlerBuilder MapCreateDriverEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/drivers",
                async (CreateDriverCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateDriver")
            .WithSummary("Register a driver identity")
            .RequirePermission(LogisticsPermissions.Drivers.Create)
            .WithIdempotency();
    }
}
