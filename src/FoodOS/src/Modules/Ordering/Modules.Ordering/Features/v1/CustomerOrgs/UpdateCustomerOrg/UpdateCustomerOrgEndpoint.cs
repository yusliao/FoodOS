using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.UpdateCustomerOrg;

public static class UpdateCustomerOrgEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCustomerOrgEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/customer-orgs/details",
                async (UpdateCustomerOrgCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(command, ct).ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("UpdateCustomerOrg")
            .WithSummary("Update an operator-owned customer organization name")
            .RequirePermission(OrderingPermissions.Customers.Update);
    }
}
