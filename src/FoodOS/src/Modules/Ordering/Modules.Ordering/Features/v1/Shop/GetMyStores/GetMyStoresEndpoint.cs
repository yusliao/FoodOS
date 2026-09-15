using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Shop.GetMyStores;

public static class GetMyStoresEndpoint
{
    internal static RouteHandlerBuilder MapGetMyStoresEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stores",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new GetMyStoresQuery(), ct))
            .WithName("GetMyShopStores")
            .WithSummary("List stores authorized for the current restaurant user")
            .RequirePermission(OrderingPermissions.Shop.View);
    }

    internal static RouteHandlerBuilder MapGetMyStoreByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/stores/{storeId:guid}",
                (Guid storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetMyStoreByIdQuery(storeId), ct))
            .WithName("GetMyShopStoreById")
            .WithSummary("Get an authorized restaurant store")
            .RequirePermission(OrderingPermissions.Shop.View);
    }
}
