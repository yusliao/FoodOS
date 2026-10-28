using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.GetCustomerLevels;

public static class GetCustomerLevelsEndpoint
{
    internal static RouteHandlerBuilder MapGetCustomerLevelsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/customer-levels",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new GetCustomerLevelsQuery(), ct))
            .WithName("GetCustomerLevels")
            .WithSummary("List customer pricing levels with their discount and customer count")
            .RequirePermission(CatalogPermissions.PriceLists.View);
    }
}
