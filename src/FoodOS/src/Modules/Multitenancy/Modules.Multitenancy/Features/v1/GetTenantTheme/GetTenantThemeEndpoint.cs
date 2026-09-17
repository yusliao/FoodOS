using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.Authorization;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using FSH.Modules.Multitenancy.Contracts.v1.GetTenantTheme;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Multitenancy.Features.v1.GetTenantTheme;

public static class GetTenantThemeEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/theme", async (string? targetTenantId, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetTenantThemeQuery(targetTenantId), cancellationToken)))
            .WithName("GetTenantTheme")
            .WithSummary("Get tenant theme")
            .WithDescription("Retrieve the current tenant theme. Root operators may specify targetTenantId without changing their identity domain.")
            .RequirePermission(MultitenancyPermissions.Tenants.ViewTheme)
            .Produces<TenantThemeDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
