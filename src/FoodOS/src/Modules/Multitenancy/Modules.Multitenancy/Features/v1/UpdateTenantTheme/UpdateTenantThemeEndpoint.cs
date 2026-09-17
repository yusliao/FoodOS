using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.Authorization;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using FSH.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Multitenancy.Features.v1.UpdateTenantTheme;

public static class UpdateTenantThemeEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/theme", async (string? targetTenantId, TenantThemeDto theme, IMediator mediator, CancellationToken cancellationToken) =>
            {
                await mediator.Send(new UpdateTenantThemeCommand(theme, targetTenantId), cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("UpdateTenantTheme")
            .WithSummary("Update tenant theme")
            .WithDescription("Update the current tenant theme. Root operators may specify targetTenantId without changing their identity domain.")
            .RequirePermission(MultitenancyPermissions.Tenants.UpdateTheme)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
