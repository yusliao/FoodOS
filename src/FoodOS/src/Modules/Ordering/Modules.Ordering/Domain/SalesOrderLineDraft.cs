using FSH.Modules.Catalog.Contracts.Dtos;

namespace FSH.Modules.Ordering.Domain;

/// <summary>
/// One priced line handed to <see cref="SalesOrder"/> when an order is drafted or amended.
/// </summary>
public sealed record SalesOrderLineDraft(
    Guid ProductId,
    string Zone,
    decimal Qty,
    decimal UnitPrice,
    string Currency,
    decimal? ListUnitPrice = null,
    string? PriceSource = null)
{
    /// <summary>Snapshots the customer's price together with the standard price and the rule that produced it.</summary>
    public static SalesOrderLineDraft FromQuote(Guid productId, string zone, decimal qty, PriceQuoteDto quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return new(productId, zone, qty, quote.UnitPrice, quote.Currency, quote.ListUnitPrice, quote.Source);
    }

    /// <summary>Lets callers that only know the price (tests, seeders) keep passing plain tuples.</summary>
    public static SalesOrderLineDraft FromValueTuple(
        (Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency) line)
        => new(line.ProductId, line.Zone, line.Qty, line.UnitPrice, line.Currency);

    public static implicit operator SalesOrderLineDraft(
        (Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency) line)
        => FromValueTuple(line);

    internal SalesOrderLine ToLine(Guid salesOrderId)
        => SalesOrderLine.Create(salesOrderId, ProductId, Zone, Qty, UnitPrice, Currency, ListUnitPrice, PriceSource);
}
