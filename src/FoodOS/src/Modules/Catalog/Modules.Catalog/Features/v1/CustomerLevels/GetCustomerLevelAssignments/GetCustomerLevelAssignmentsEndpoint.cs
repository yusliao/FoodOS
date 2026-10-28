using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.GetCustomerLevelAssignments;

public static class GetCustomerLevelAssignmentsEndpoint
{
    internal static RouteHandlerBuilder MapGetCustomerLevelAssignmentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/customer-levels/assignments",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new GetCustomerLevelAssignmentsQuery(), ct))
            .WithName("GetCustomerLevelAssignments")
            .WithSummary("List which pricing level each customer organization belongs to")
            .RequirePermission(CatalogPermissions.PriceLists.View);
    }
}
