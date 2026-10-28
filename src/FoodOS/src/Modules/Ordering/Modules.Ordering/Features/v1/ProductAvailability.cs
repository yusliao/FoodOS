using FSH.Modules.WmsIntegration.Contracts.v1;

namespace FSH.Modules.Ordering.Features.v1;

/// <summary>WMS sellable-availability lookup shared by Shop and order desk product listings.</summary>
internal static class ProductAvailability
{
    public static async Task<IReadOnlyDictionary<(string Sku, string Uom), WmsAvailabilityResult>> GetAsync(
        IWmsAvailabilityReader reader,
        string? warehouseCode,
        IReadOnlyCollection<(string Sku, string Uom, decimal Quantity)> products,
        CancellationToken cancellationToken)
    {
        if (warehouseCode is null)
        {
            return products
                .DistinctBy(product => (Normalize(product.Sku), Normalize(product.Uom)))
                .ToDictionary(
                    product => (Normalize(product.Sku), Normalize(product.Uom)),
                    product => new WmsAvailabilityResult(product.Sku, product.Uom, 0, false, null, "warehouseMissing"));
        }

        var result = await reader.GetAvailabilityAsync(
                warehouseCode,
                products.Select(product => new WmsAvailabilityRequest(
                    product.Sku,
                    product.Uom,
                    product.Quantity)).ToList(),
                cancellationToken)
            .ConfigureAwait(false);
        return result
            .DistinctBy(item => (Normalize(item.Sku), Normalize(item.Uom)))
            .ToDictionary(
                item => (Normalize(item.Sku), Normalize(item.Uom)),
                item => item);
    }

    public static WmsAvailabilityResult For(
        IReadOnlyDictionary<(string Sku, string Uom), WmsAvailabilityResult> availability,
        string sku,
        string uom) => availability[(Normalize(sku), Normalize(uom))];

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
