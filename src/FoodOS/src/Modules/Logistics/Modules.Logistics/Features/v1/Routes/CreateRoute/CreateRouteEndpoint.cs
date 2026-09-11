using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Routes.CreateRoute;

public static class CreateRouteEndpoint
{
    internal static RouteHandlerBuilder MapCreateRouteEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/routes",
                async (CreateRouteCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateRoute")
            .WithSummary("Create a fixed delivery route")
            .RequirePermission(LogisticsPermissions.Routes.Create)
            .WithIdempotency();
    }
}
