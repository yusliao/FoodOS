using FSH.Framework.Core.Domain;
using FSH.Modules.Catalog.Domain.Events;

namespace FSH.Modules.Catalog.Domain;

public sealed class Product : AggregateRoot<Guid>, ISoftDeletable
{
    public string Sku { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid BrandId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Money Price { get; private set; } = default!;
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }
    public TemperatureZone TemperatureZone { get; private set; }
    public int? ShelfLifeDays { get; private set; }
    public int MinRemainingDaysOnShip { get; private set; }
    public string BaseUom { get; private set; } = "EA";
    public bool CatchWeight { get; private set; }
    public string? Barcode { get; private set; }
    public string? StorageNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    // EF populates this via the navigation property; aggregate methods mutate through the
    // private list so invariants (single thumbnail, contiguous SortOrder) hold.
    private readonly List<ProductImage> _images = [];
    public IReadOnlyList<ProductImage> Images => _images;

    private readonly List<ProductTranslation> _translations = [];
    public IReadOnlyList<ProductTranslation> Translations => _translations;

    /// <summary>The thumbnail (cover) image URL, or null when the product has no images.</summary>
    public string? ThumbnailUrl => _images.FirstOrDefault(i => i.IsThumbnail)?.Url;

    public void Restore()
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        DeletedOnUtc = null;
        DeletedBy = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private Product() { }

    public static Product Create(
        string sku,
        string name,
        string? description,
        Guid brandId,
        Guid categoryId,
        Money price,
        int stock,
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        int? shelfLifeDays = null,
        int minRemainingDaysOnShip = 0,
        string baseUom = "EA",
        bool catchWeight = false,
        string? barcode = null,
        string? storageNote = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(price);
        if (stock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stock), "Stock cannot be negative.");
        }
        if (brandId == Guid.Empty)
        {
            throw new ArgumentException("BrandId is required.", nameof(brandId));
        }
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }

        if (minRemainingDaysOnShip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minRemainingDaysOnShip), "Min remaining days cannot be negative.");
        }

        if (shelfLifeDays is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shelfLifeDays), "Shelf life cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(baseUom);

        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Slug = Slugify(name),
            Description = description?.Trim(),
            BrandId = brandId,
            CategoryId = categoryId,
            Price = price,
            Stock = stock,
            IsActive = true,
            TemperatureZone = temperatureZone,
            ShelfLifeDays = shelfLifeDays,
            MinRemainingDaysOnShip = minRemainingDaysOnShip,
            BaseUom = baseUom.Trim().ToUpperInvariant(),
            CatchWeight = catchWeight,
            Barcode = barcode?.Trim(),
            StorageNote = storageNote?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        product.AddDomainEvent(DomainEvent.Create((id, ts) =>
            new ProductCreatedDomainEvent(product.Id, product.Sku, product.Name, id, ts)));

        return product;
    }

    public void Update(
        string name,
        string? description,
        Guid brandId,
        Guid categoryId,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (brandId == Guid.Empty)
        {
            throw new ArgumentException("BrandId is required.", nameof(brandId));
        }
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }

        Name = name.Trim();
        Slug = Slugify(name);
        Description = description?.Trim();
        BrandId = brandId;
        CategoryId = categoryId;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Foodservice fulfillment attributes. Stock on this aggregate is not the available-to-promise quantity;
    /// that lives in the Inventory module (warehouse × zone × lot).
    /// </summary>
    public void SetFulfillmentAttributes(
        TemperatureZone temperatureZone,
        int? shelfLifeDays,
        int minRemainingDaysOnShip,
        string baseUom,
        bool catchWeight,
        string? barcode,
        string? storageNote)
    {
        if (minRemainingDaysOnShip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minRemainingDaysOnShip), "Min remaining days cannot be negative.");
        }

        if (shelfLifeDays is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shelfLifeDays), "Shelf life cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(baseUom);

        TemperatureZone = temperatureZone;
        ShelfLifeDays = shelfLifeDays;
        MinRemainingDaysOnShip = minRemainingDaysOnShip;
        BaseUom = baseUom.Trim().ToUpperInvariant();
        CatchWeight = catchWeight;
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        StorageNote = storageNote?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public ProductTranslation UpsertTranslation(string culture, string name, string? description)
    {
        string normalized = ProductTranslation.NormalizeCulture(culture);
        var existing = _translations.FirstOrDefault(t =>
            string.Equals(t.Culture, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(name, description);
            UpdatedAtUtc = DateTime.UtcNow;
            return existing;
        }

        var translation = ProductTranslation.Create(Id, normalized, name, description);
        _translations.Add(translation);
        UpdatedAtUtc = DateTime.UtcNow;
        return translation;
    }

    public (string Name, string? Description) ResolveLocalizedCopy(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return (Name, Description);
        }

        var exact = _translations.FirstOrDefault(t =>
            string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return (exact.Name, exact.Description);
        }

        string language = culture.Split('-', 2)[0];
        var languageMatch = _translations.FirstOrDefault(t =>
            t.Culture.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.Culture, language, StringComparison.OrdinalIgnoreCase));
        if (languageMatch is not null)
        {
            return (languageMatch.Name, languageMatch.Description);
        }

        return (Name, Description);
    }

    public void ChangePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        if (newPrice == Price)
        {
            return;
        }

        decimal oldAmount = Price.Amount;
        Price = newPrice;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(DomainEvent.Create((id, ts) =>
            new ProductPriceChangedDomainEvent(Id, oldAmount, newPrice.Amount, newPrice.Currency, id, ts)));
    }

    public void AdjustStock(int delta)
    {
        int newStock = Stock + delta;
        if (newStock < 0)
        {
            throw new InvalidOperationException(
                $"Stock adjustment of {delta} would result in negative stock (current: {Stock}).");
        }

        int oldStock = Stock;
        Stock = newStock;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(DomainEvent.Create((id, ts) =>
            new ProductStockAdjustedDomainEvent(Id, oldStock, newStock, delta, id, ts)));
    }

    // ─── Image management ─────────────────────────────────────────────────

    /// <summary>
    /// Attach a new image. The first image attached is automatically the thumbnail; subsequent
    /// images come in non-thumbnail and the caller can promote one via <see cref="SetThumbnail"/>.
    /// </summary>
    public ProductImage AddImage(Guid? fileAssetId, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        bool isFirst = _images.Count == 0;
        int order = isFirst ? 0 : _images.Max(i => i.SortOrder) + 1;
        var image = ProductImage.Create(Id, fileAssetId, url, isThumbnail: isFirst, sortOrder: order);
        _images.Add(image);
        UpdatedAtUtc = DateTime.UtcNow;
        return image;
    }

    /// <summary>
    /// Remove an image. If the removed image was the thumbnail and other images remain, the
    /// lowest-sorted remaining image is promoted to thumbnail so the product always has a cover.
    /// </summary>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Image {imageId} not found on product {Id}.");
        bool wasThumbnail = image.IsThumbnail;
        _images.Remove(image);

        if (wasThumbnail && _images.Count > 0)
        {
            var promoted = _images.OrderBy(i => i.SortOrder).First();
            promoted.MarkThumbnail(true);
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Mark <paramref name="imageId"/> as the thumbnail; clears the flag on every other image.</summary>
    public void SetThumbnail(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Image {imageId} not found on product {Id}.");
        if (image.IsThumbnail) return;

        foreach (var i in _images)
        {
            i.MarkThumbnail(i.Id == imageId);
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reorder images by the supplied id sequence. Ids not in <paramref name="orderedImageIds"/> are appended in their existing order after the rest.</summary>
    public void ReorderImages(IReadOnlyList<Guid> orderedImageIds)
    {
        ArgumentNullException.ThrowIfNull(orderedImageIds);
        int order = 0;
        var seen = new HashSet<Guid>();
        foreach (var id in orderedImageIds)
        {
            var image = _images.FirstOrDefault(i => i.Id == id);
            if (image is null) continue;
            image.SetSortOrder(order++);
            seen.Add(id);
        }
        foreach (var trailing in _images.Where(i => !seen.Contains(i.Id)).OrderBy(i => i.SortOrder).ToList())
        {
            trailing.SetSortOrder(order++);
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Slugify(string value)
    {
        var trimmed = value.Trim();
#pragma warning disable CA1308 // slug is canonical lowercase, not security-sensitive
        var lower = trimmed.ToLowerInvariant();
#pragma warning restore CA1308
        var chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var collapsed = new string(chars).Trim('-');
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }
        return collapsed;
    }
}
