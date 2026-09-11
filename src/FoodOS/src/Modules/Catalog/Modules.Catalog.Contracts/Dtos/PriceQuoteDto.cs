namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceQuoteDto(
    Guid CustomerOrgId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    string Source);
