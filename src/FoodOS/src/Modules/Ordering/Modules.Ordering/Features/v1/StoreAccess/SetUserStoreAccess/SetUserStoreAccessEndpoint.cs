using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.StoreAccess.SetUserStoreAccess;

public static class SetUserStoreAccessEndpoint
{
    internal static RouteHandlerBuilder MapSetUserStoreAccessEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPut("/store-access/users/{userId:guid}", async (
                Guid userId,
                SetUserStoreAccessCommand command,
                IMediator mediator,
                CancellationToken ct) =>
            {
                await mediator.Send(command with { UserId = userId }, ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("SetUserStoreAccess")
            .WithSummary("Replace a restaurant user's authorized store set")
            .RequirePermission(OrderingPermissions.StoreAccess.Manage);
}
