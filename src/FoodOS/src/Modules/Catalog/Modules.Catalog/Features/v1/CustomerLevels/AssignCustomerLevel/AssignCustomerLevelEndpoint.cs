using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.AssignCustomerLevel;

public static class AssignCustomerLevelEndpoint
{
    internal static RouteHandlerBuilder MapAssignCustomerLevelEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/customer-levels/assignments/{customerOrgId:guid}",
                async (Guid customerOrgId, AssignCustomerLevelRequest request, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new AssignCustomerLevelCommand(customerOrgId, request.CustomerLevelId), ct)
                        .ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("AssignCustomerLevel")
            .WithSummary("Put a customer organization into a pricing level, or clear it with a null level")
            .RequirePermission(CatalogPermissions.PriceLists.Update);
    }
}

public sealed record AssignCustomerLevelRequest(Guid? CustomerLevelId);
