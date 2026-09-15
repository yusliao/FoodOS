namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid BrandId,
    Guid CategoryId,
    decimal UnitPrice,
    string Currency,
    string PriceSource,
    string BaseUom,
    bool CatchWeight,
    string? ThumbnailUrl,
    bool IsAvailable);
