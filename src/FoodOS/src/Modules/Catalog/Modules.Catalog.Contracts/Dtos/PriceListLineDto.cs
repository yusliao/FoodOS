namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceListLineDto(
    Guid Id,
    Guid ProductId,
    decimal MinQty,
    decimal UnitPrice,
    string Currency)
{
    /// <summary>Product SKU for display; null when the product no longer exists.</summary>
    public string? ProductSku { get; init; }

    /// <summary>Product name for display; null when the product no longer exists.</summary>
    public string? ProductName { get; init; }
}
