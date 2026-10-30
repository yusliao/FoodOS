using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists;

internal static class PriceListMappings
{
    /// <summary>Maps the lists and fills each line's product SKU and name in one product query.</summary>
    public static async Task<IReadOnlyList<PriceListDto>> ToDtosAsync(
        this IReadOnlyCollection<PriceList> lists,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(dbContext);

        var productIds = lists.SelectMany(list => list.Lines).Select(line => line.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<Guid, (string Sku, string Name)>()
            : await dbContext.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .Select(product => new { product.Id, product.Sku, product.Name })
                .ToDictionaryAsync(product => product.Id, product => (product.Sku, product.Name), cancellationToken)
                .ConfigureAwait(false);

        return lists.Select(list => list.ToDto(products)).ToList();
    }

    private static PriceListDto ToDto(this PriceList list, IReadOnlyDictionary<Guid, (string Sku, string Name)> products)
        => new(
            list.Id,
            list.Name,
            list.CustomerOrgId,
            list.Priority,
            list.ValidFrom,
            list.ValidTo,
            list.Lines
                .OrderBy(l => l.ProductId)
                .ThenBy(l => l.MinQty)
                .Select(l =>
                {
                    bool found = products.TryGetValue(l.ProductId, out var product);
                    return new PriceListLineDto(l.Id, l.ProductId, l.MinQty, l.UnitPrice, l.Currency)
                    {
                        ProductSku = found ? product.Sku : null,
                        ProductName = found ? product.Name : null,
                    };
                })
                .ToList())
        {
            CustomerLevelId = list.CustomerLevelId,
        };
}
