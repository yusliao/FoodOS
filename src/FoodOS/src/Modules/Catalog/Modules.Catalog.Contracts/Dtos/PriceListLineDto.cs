namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceListLineDto(
    Guid Id,
    Guid ProductId,
    decimal MinQty,
    decimal UnitPrice,
    string Currency);
