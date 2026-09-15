using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace FSH.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public static class GenerateTokenEndpoint
{
    /// <summary>
    /// Header used by clients to identify which app shell is requesting the token.
    /// SuperAdmin (root tenant) accounts are restricted to the admin app — submitting
    /// "dashboard" with tenant=root yields a 403 instead of a useful token. This is a
    /// belt-and-braces check; the dashboard client also rejects root-tenant tokens
    /// locally for a cleaner UX.
    /// </summary>
    public const string AppHeader = "X-FSH-App";
    public const string AppAdmin = "admin";
    public const string AppDashboard = "dashboard";

    public static RouteHandlerBuilder MapGenerateTokenEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/issue",
            [AllowAnonymous] async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
            ([FromBody] GenerateTokenCommand command,
            [DefaultValue("root")][FromHeader] string tenant,
            [FromHeader(Name = AppHeader)] string? app,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
            {
                var boundaryError = GetAppBoundaryError(tenant, app);
                if (boundaryError is not null)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "App boundary",
                        detail: boundaryError);
                }

                var token = await mediator.Send(command, ct);
                return token is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(token);
            })
            .WithName("IssueJwtTokens")
            .WithSummary("Issue JWT access and refresh tokens")
            .WithDescription("Submit credentials to receive a JWT access token and a refresh token. Provide the 'tenant' header to select the tenant context (defaults to 'root'). The 'X-FSH-App' header (admin|dashboard) is used to enforce the SuperAdmin / dashboard boundary.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    public static string? GetAppBoundaryError(string tenant, string? app)
    {
        bool isRoot = string.Equals(tenant, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase);
        if (isRoot && string.Equals(app, AppDashboard, StringComparison.OrdinalIgnoreCase))
        {
            return "Operator accounts must use the admin app. Sign in there instead of the restaurant dashboard.";
        }

        if (!isRoot && string.Equals(app, AppAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return "Restaurant tenant accounts must use the dashboard app.";
        }

        return null;
    }
}
