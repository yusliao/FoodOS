using System.Globalization;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Features.v1.Products;

internal static class ProductMappings
{
    public static ProductDto ToDto(this Product p)
    {
        var localized = p.ResolveLocalizedCopy(CultureInfo.CurrentUICulture.Name);
        return new ProductDto(
            p.Id,
            p.Sku,
            localized.Name,
            p.Slug,
            localized.Description,
            p.BrandId,
            p.CategoryId,
            new MoneyDto(p.Price.Amount, p.Price.Currency),
            p.Stock,
            p.IsActive,
            p.TemperatureZone.ToString(),
            p.ShelfLifeDays,
            p.MinRemainingDaysOnShip,
            p.BaseUom,
            p.CatchWeight,
            p.Barcode,
            p.StorageNote,
            p.ThumbnailUrl,
            p.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.Id, i.FileAssetId, i.Url, i.IsThumbnail, i.SortOrder, i.CreatedAtUtc))
                .ToList(),
            p.Translations
                .Select(t => new ProductTranslationDto(t.Culture, t.Name, t.Description))
                .ToList(),
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            p.DeletedOnUtc,
            p.DeletedBy);
    }
}
