using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertPriceListLine;

public static class UpsertPriceListLineEndpoint
{
    internal static RouteHandlerBuilder MapUpsertPriceListLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/price-lists/{priceListId:guid}/lines",
                async (Guid priceListId, UpsertPriceListLineCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { PriceListId = priceListId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UpsertPriceListLine")
            .WithSummary("Add or replace a quantity-tier line on a price list")
            .RequirePermission(CatalogPermissions.PriceLists.Update)
            .WithIdempotency();
    }
}
