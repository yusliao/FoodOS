using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Stores.GetStores;

public static class GetStoresEndpoint
{
    internal static RouteHandlerBuilder MapGetStoresEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stores",
                (Guid? customerOrgId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetStoresQuery(customerOrgId), ct))
            .WithName("GetStores")
            .WithSummary("List stores for operator administration")
            .RequirePermission(OrderingPermissions.Stores.View);
    }
}
