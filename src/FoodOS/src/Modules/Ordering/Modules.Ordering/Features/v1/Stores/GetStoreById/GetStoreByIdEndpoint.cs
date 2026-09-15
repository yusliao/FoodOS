using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Stores.GetStoreById;

public static class GetStoreByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetStoreByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stores/{storeId:guid}",
                (Guid storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetStoreByIdQuery(storeId), ct))
            .WithName("GetStoreById")
            .WithSummary("Get a store by id")
            .RequirePermission(OrderingPermissions.Stores.View);
    }
}
