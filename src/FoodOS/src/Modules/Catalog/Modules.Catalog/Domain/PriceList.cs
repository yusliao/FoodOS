using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Customer contract or catalog-wide (null <see cref="CustomerOrgId"/>) price list with quantity tiers.
/// </summary>
public sealed class PriceList : AggregateRoot<Guid>
{
    private readonly List<PriceListLine> _lines = [];

    public string Name { get; private set; } = default!;
    public Guid? CustomerOrgId { get; private set; }
    public int Priority { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }

    public IReadOnlyList<PriceListLine> Lines => _lines;

    private PriceList() { }

    public static PriceList Create(
        string name,
        Guid? customerOrgId,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo = null,
        int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (validFrom == default)
        {
            throw new ArgumentException("ValidFrom is required.", nameof(validFrom));
        }

        if (validTo is { } to && to < validFrom)
        {
            throw new ArgumentException("ValidTo cannot be earlier than ValidFrom.", nameof(validTo));
        }

        Guid? orgId = customerOrgId is { } id && id != Guid.Empty ? id : null;

        return new PriceList
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CustomerOrgId = orgId,
            Priority = priority,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
    }

    public bool IsValidAt(DateTimeOffset asOf)
        => ValidFrom <= asOf && (ValidTo is null || ValidTo >= asOf);

    public PriceListLine UpsertLine(Guid productId, decimal minQty, decimal unitPrice, string currency)
    {
        var existing = _lines.FirstOrDefault(l => l.ProductId == productId && l.MinQty == minQty);
        if (existing is not null)
        {
            existing.Update(unitPrice, currency);
            return existing;
        }

        var line = PriceListLine.Create(Id, productId, minQty, unitPrice, currency);
        _lines.Add(line);
        return line;
    }
}
