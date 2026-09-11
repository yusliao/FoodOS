using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.SearchCustomerOrgs;

public static class SearchCustomerOrgsEndpoint
{
    internal static RouteHandlerBuilder MapSearchCustomerOrgsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/customer-orgs",
                (string? search, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchCustomerOrgsQuery(search), ct))
            .WithName("SearchCustomerOrgs")
            .WithSummary("Search customer organizations")
            .RequirePermission(OrderingPermissions.Customers.View);
    }
}
