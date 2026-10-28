namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Resolves unit price at order time:
/// lock → customer contract tier → level price list → level discount on (catalog list / product price) → catalog price.
/// Caller must pass only lists that belong to the quoted customer, the customer's level, or the whole catalog.
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
        IEnumerable<PriceList> lists,
        CustomerLevel? level = null)
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

        if (level is not null)
        {
            var levelLine = MatchTier(
                valid.Where(l => l.CustomerOrgId is null && l.CustomerLevelId == level.Id),
                productId,
                quantity);
            if (levelLine is not null)
            {
                return (levelLine.UnitPrice, levelLine.Currency, PriceQuoteSource.Level);
            }
        }

        var (basePrice, currency) = ListPrice(productId, catalogPrice, quantity, asOf, valid);

        return level is { DiscountPercent: > 0 }
            ? (level.ApplyDiscount(basePrice), currency, PriceQuoteSource.Level)
            : (basePrice, currency, PriceQuoteSource.Catalog);
    }

    /// <summary>
    /// Standard price before any customer-specific pricing: catalog-wide tier, else product price.
    /// Level lists are excluded so one level's prices never leak into the standard price.
    /// </summary>
    public static (decimal UnitPrice, string Currency) ListPrice(
        Guid productId,
        Money catalogPrice,
        decimal quantity,
        DateTimeOffset asOf,
        IEnumerable<PriceList> lists)
    {
        ArgumentNullException.ThrowIfNull(catalogPrice);
        ArgumentNullException.ThrowIfNull(lists);
        var catalogLine = MatchTier(
            lists.Where(l => l.IsValidAt(asOf) && l.CustomerOrgId is null && l.CustomerLevelId is null),
            productId,
            quantity);
        return catalogLine is not null
            ? (catalogLine.UnitPrice, catalogLine.Currency)
            : (catalogPrice.Amount, catalogPrice.Currency);
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
