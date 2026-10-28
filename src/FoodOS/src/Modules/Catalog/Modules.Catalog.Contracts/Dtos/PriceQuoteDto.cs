namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceQuoteDto(
    Guid CustomerOrgId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    string Source)
{
    /// <summary>
    /// Standard unit price an unclassified customer would pay (catalog-wide tier or product price),
    /// so screens can show the original price next to the customer's price.
    /// </summary>
    public decimal? ListUnitPrice { get; init; }
}
