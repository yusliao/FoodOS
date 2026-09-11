using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.GetPriceListById;

public static class GetPriceListByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetPriceListByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/price-lists/{priceListId:guid}",
                (Guid priceListId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetPriceListByIdQuery(priceListId), ct))
            .WithName("GetPriceListById")
            .WithSummary("Get a price list and its quantity tiers")
            .RequirePermission(CatalogPermissions.PriceLists.View);
    }
}
