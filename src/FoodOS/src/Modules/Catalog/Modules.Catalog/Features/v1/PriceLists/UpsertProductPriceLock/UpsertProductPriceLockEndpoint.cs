using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertProductPriceLock;

public static class UpsertProductPriceLockEndpoint
{
    internal static RouteHandlerBuilder MapUpsertProductPriceLockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/price-locks",
                async (UpsertProductPriceLockCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("UpsertProductPriceLock")
            .WithSummary("Lock a unit price for a customer and SKU until a given instant")
            .RequirePermission(CatalogPermissions.PriceLists.Update)
            .WithIdempotency();
    }
}
