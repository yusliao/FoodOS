using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;

public static class CreateCustomerOrgEndpoint
{
    internal static RouteHandlerBuilder MapCreateCustomerOrgEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/customer-orgs",
                async (CreateCustomerOrgCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateCustomerOrg")
            .WithSummary("Create a customer organization")
            .RequirePermission(OrderingPermissions.Customers.Create)
            .WithIdempotency();
    }
}
