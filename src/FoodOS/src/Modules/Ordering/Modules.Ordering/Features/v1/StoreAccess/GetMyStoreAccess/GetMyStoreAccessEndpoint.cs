using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.StoreAccess.GetMyStoreAccess;

public static class GetMyStoreAccessEndpoint
{
    internal static RouteHandlerBuilder MapGetMyStoreAccessEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/store-access/me", (IMediator mediator, CancellationToken ct) =>
                mediator.Send(new GetMyStoreAccessQuery(), ct))
            .WithName("GetMyStoreAccess")
            .WithSummary("Get the current restaurant user's authorized stores")
            .RequirePermission(OrderingPermissions.StoreAccess.View);
}
