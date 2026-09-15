using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1;

internal static class ShopCatalog
{
    public static async Task<IReadOnlyDictionary<Guid, (ProductDto Product, TemperatureZoneKind Zone)>> GetActiveManyAsync(
        IMediator mediator,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();
        var products = await mediator.Send(new GetProductsByIdsQuery(ids), cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<Guid, (ProductDto Product, TemperatureZoneKind Zone)>(products.Count);
        foreach (var product in products)
        {
            if (!product.IsActive)
            {
                throw new CustomException(
                    $"Product {product.Id} is not available.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.Conflict);
            }

            if (!Enum.TryParse(product.TemperatureZone, ignoreCase: true, out TemperatureZoneKind zone))
            {
                throw new CustomException(
                    string.Create(CultureInfo.InvariantCulture, $"Unknown temperature zone '{product.TemperatureZone}'."),
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            }

            result.Add(product.Id, (product, zone));
        }

        return result;
    }

    public static async Task<IReadOnlyDictionary<Guid, PriceQuoteDto>> QuoteManyAsync(
        IMediator mediator,
        Guid customerOrgId,
        IEnumerable<(Guid ProductId, decimal Quantity)> lines,
        CancellationToken cancellationToken)
    {
        var requests = lines
            .Select(line => new ProductPriceRequestDto(line.ProductId, line.Quantity))
            .ToList();
        var quotes = await mediator.Send(
                new QuoteProductPricesQuery(customerOrgId, requests),
                cancellationToken)
            .ConfigureAwait(false);
        return quotes.ToDictionary(quote => quote.ProductId);
    }
}
