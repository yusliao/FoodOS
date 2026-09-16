using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.v1.Impersonation;
using FSH.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Impersonation.StartImpersonation;

public static class StartImpersonationEndpoint
{
    internal static RouteHandlerBuilder MapStartImpersonationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/start",
            async Task<Results<Ok<ImpersonationResponse>, ProblemHttpResult>>
            ([FromBody] StartImpersonationCommand command,
             [FromServices] IMediator mediator,
             CancellationToken ct) =>
            {
                var response = await mediator.Send(command, ct);
                return TypedResults.Ok(response);
            })
            .WithName("StartImpersonation")
            .WithSummary("Start user impersonation")
            .WithDescription("Authorized root operators may issue a short-lived target-user token carrying actor claims (act_sub, act_tenant). Customer identities cannot start impersonation, including within their own tenant. No refresh token is issued.")
            .RequirePermission(IdentityPermissions.Users.Impersonate)
            .Produces<ImpersonationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
