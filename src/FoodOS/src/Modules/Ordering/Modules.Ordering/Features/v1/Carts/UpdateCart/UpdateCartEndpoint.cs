using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Carts.UpdateCart;

public static class UpdateCartEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCartEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/carts/{storeId:guid}",
                async (Guid storeId, UpdateCartCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command with { StoreId = storeId }, ct).ConfigureAwait(false)))
            .WithName("UpdateCart")
            .WithSummary("Replace the cart for a store")
            .RequirePermission(OrderingPermissions.Orders.Manage);
    }
}
