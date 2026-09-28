using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Stores.UpdateStore;

public static class UpdateStoreEndpoint
{
    internal static RouteHandlerBuilder MapUpdateStoreEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/stores/details",
                async (UpdateStoreCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(command, ct).ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("UpdateStore")
            .WithSummary("Update an operator-owned store's name and delivery address")
            .RequirePermission(OrderingPermissions.Stores.Update);
    }
}
