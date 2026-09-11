using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.QuoteProductPrice;

public static class GetProductPriceQuoteEndpoint
{
    internal static RouteHandlerBuilder MapGetProductPriceQuoteEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/quotes",
                (Guid customerOrgId, Guid productId, decimal quantity, DateTimeOffset? asOf, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new QuoteProductPriceQuery(customerOrgId, productId, quantity, asOf), ct))
            .WithName("GetProductPriceQuote")
            .WithSummary("Resolve unit price for a customer and SKU (lock → contract tier → catalog)")
            .RequirePermission(CatalogPermissions.Products.View);
    }
}
