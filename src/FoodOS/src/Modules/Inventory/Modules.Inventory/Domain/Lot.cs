using FSH.Framework.Core.Domain;

namespace FSH.Modules.Inventory.Domain;

public sealed class Lot : AggregateRoot<Guid>
{
    public string LotNo { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public DateOnly? ManufacturedOn { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public string? Origin { get; private set; }
    public LotStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Lot() { }

    public static Lot Create(
        string lotNo,
        Guid productId,
        DateOnly expiryDate,
        DateOnly? manufacturedOn = null,
        Guid? supplierId = null,
        string? origin = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotNo);
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        return new Lot
        {
            Id = Guid.CreateVersion7(),
            LotNo = lotNo.Trim().ToUpperInvariant(),
            ProductId = productId,
            SupplierId = supplierId,
            ManufacturedOn = manufacturedOn,
            ExpiryDate = expiryDate,
            Origin = origin?.Trim(),
            Status = LotStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Isolate() => Status = LotStatus.Isolated;

    public void MarkExhausted() => Status = LotStatus.Exhausted;
}
