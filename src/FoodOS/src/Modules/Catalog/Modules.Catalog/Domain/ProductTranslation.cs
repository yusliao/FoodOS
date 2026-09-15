using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Localized name/description for a product. Canonical (default-culture) copy lives on <see cref="Product"/>.
/// </summary>
public sealed class ProductTranslation : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid ProductId { get; private set; }
    public string Culture { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    private ProductTranslation() { }

    public static ProductTranslation Create(Guid productId, string culture, string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ProductTranslation
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Culture = NormalizeCulture(culture),
            Name = name.Trim(),
            Description = description?.Trim()
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
    }

    public static string NormalizeCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        return culture.Trim();
    }
}
