using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class ReceiveRecord : BaseEntity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid QualityCheckId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Zone { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ReceiveRecord() { }

    internal static ReceiveRecord Create(Guid purchaseOrderId, Guid qualityCheckId, Guid lotId, decimal quantity, string zone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new ReceiveRecord
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            QualityCheckId = qualityCheckId,
            LotId = lotId,
            Quantity = quantity,
            Zone = zone.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
