namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Resolves unit price at order time: lock → customer contract tier → catalog list / product list price.
/// Caller must pass only lists that belong to the quoted customer or have a null customer (catalog-wide).
/// </summary>
public static class PriceResolver
{
    public static (decimal UnitPrice, string Currency, string Source) Resolve(
        Guid customerOrgId,
        Guid productId,
        Money catalogPrice,
        decimal quantity,
        DateTimeOffset asOf,
        ProductContractLock? priceLock,
        IEnumerable<PriceList> lists)
    {
        ArgumentNullException.ThrowIfNull(catalogPrice);
        ArgumentNullException.ThrowIfNull(lists);
        if (customerOrgId == Guid.Empty)
        {
            throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (priceLock is not null
            && priceLock.CustomerOrgId == customerOrgId
            && priceLock.ProductId == productId
            && priceLock.IsActive(asOf))
        {
            return (priceLock.UnitPrice, priceLock.Currency, PriceQuoteSource.Locked);
        }

        var valid = lists.Where(l => l.IsValidAt(asOf)).ToList();

        var contractLine = MatchTier(
            valid.Where(l => l.CustomerOrgId == customerOrgId),
            productId,
            quantity);
        if (contractLine is not null)
        {
            return (contractLine.UnitPrice, contractLine.Currency, PriceQuoteSource.Contract);
        }

        var catalogLine = MatchTier(
            valid.Where(l => l.CustomerOrgId is null),
            productId,
            quantity);
        if (catalogLine is not null)
        {
            return (catalogLine.UnitPrice, catalogLine.Currency, PriceQuoteSource.Catalog);
        }

        return (catalogPrice.Amount, catalogPrice.Currency, PriceQuoteSource.Catalog);
    }

    private static PriceListLine? MatchTier(IEnumerable<PriceList> lists, Guid productId, decimal quantity)
    {
        foreach (var list in lists.OrderByDescending(l => l.Priority))
        {
            var line = list.Lines
                .Where(l => l.ProductId == productId && l.MinQty <= quantity)
                .OrderByDescending(l => l.MinQty)
                .FirstOrDefault();
            if (line is not null)
            {
                return line;
            }
        }

        return null;
    }
}
