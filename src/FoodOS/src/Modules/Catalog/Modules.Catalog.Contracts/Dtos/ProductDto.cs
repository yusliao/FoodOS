namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Slug,
    string? Description,
    Guid BrandId,
    Guid CategoryId,
    MoneyDto Price,
    int Stock,
    bool IsActive,
    string TemperatureZone,
    int? ShelfLifeDays,
    int MinRemainingDaysOnShip,
    string BaseUom,
    bool CatchWeight,
    string? Barcode,
    string? StorageNote,
    string? ThumbnailUrl,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductTranslationDto> Translations,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTimeOffset? DeletedOnUtc = null,
    string? DeletedBy = null)
{
    /// <summary>Filled by list queries so operators read names instead of ids.</summary>
    public string? BrandName { get; init; }
    public string? CategoryName { get; init; }
}
