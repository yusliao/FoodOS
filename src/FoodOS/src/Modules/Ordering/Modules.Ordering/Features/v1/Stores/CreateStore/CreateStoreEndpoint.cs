using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Stores.CreateStore;

public static class CreateStoreEndpoint
{
    internal static RouteHandlerBuilder MapCreateStoreEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stores",
                async (CreateStoreCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateStore")
            .WithSummary("Create a store for a customer organization")
            .RequirePermission(OrderingPermissions.Stores.Create)
            .WithIdempotency();
    }
}
