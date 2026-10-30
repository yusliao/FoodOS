using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.DeletePriceListLine;

public static class DeletePriceListLineEndpoint
{
    internal static RouteHandlerBuilder MapDeletePriceListLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/price-lists/{priceListId:guid}/lines/{lineId:guid}",
                async (Guid priceListId, Guid lineId, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeletePriceListLineCommand(priceListId, lineId), ct);
                    return Results.NoContent();
                })
            .WithName("DeletePriceListLine")
            .WithSummary("Remove a quantity-tier line from a price list")
            .RequirePermission(CatalogPermissions.PriceLists.Update);
    }
}
