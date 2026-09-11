using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class Cart : AggregateRoot<Guid>
{
    private readonly List<CartLine> _lines = [];

    public Guid StoreId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<CartLine> Lines => _lines;

    private Cart() { }

    public static Cart Create(Guid storeId)
    {
        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("StoreId is required.", nameof(storeId));
        }

        return new Cart
        {
            Id = Guid.CreateVersion7(),
            StoreId = storeId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void ReplaceLines(IReadOnlyList<(Guid ProductId, decimal Quantity, string Zone)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines.Clear();
        foreach (var (productId, quantity, zone) in lines)
        {
            _lines.Add(CartLine.Create(Id, productId, quantity, zone));
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        _lines.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
