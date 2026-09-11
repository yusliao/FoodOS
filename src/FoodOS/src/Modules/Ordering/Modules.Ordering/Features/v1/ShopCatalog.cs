using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1;

internal static class ShopCatalog
{
    public static async Task<(ProductDto Product, TemperatureZoneKind Zone)> GetActiveAsync(
        IMediator mediator,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await mediator.Send(new GetProductByIdQuery(productId), cancellationToken).ConfigureAwait(false);
        if (!product.IsActive)
        {
            throw new CustomException(
                $"Product {productId} is not available.",
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

        return (product, zone);
    }
}
