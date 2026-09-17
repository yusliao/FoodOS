using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Modules.Identity.Contracts.v1.Impersonation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Impersonation.SearchImpersonationUsers;

public static class SearchImpersonationUsersEndpoint
{
    internal static RouteHandlerBuilder MapSearchImpersonationUsersEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/impersonation/users",
            async ([AsParameters] SearchImpersonationUsersQuery query, IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(query, ct).ConfigureAwait(false)))
            .WithName("SearchImpersonationUsers")
            .WithSummary("Find eligible users in an explicit support target tenant")
            .RequirePermission(IdentityPermissions.Users.Impersonate);
}
