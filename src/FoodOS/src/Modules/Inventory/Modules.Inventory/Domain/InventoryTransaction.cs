using FSH.Framework.Core.Domain;

namespace FSH.Modules.Inventory.Domain;

public sealed class InventoryTransaction : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public InventoryTransactionType Type { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public InventoryBucket? FromBucket { get; private set; }
    public InventoryBucket? ToBucket { get; private set; }
    public string? RefType { get; private set; }
    public Guid? RefId { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }

    private InventoryTransaction() { }

    public static InventoryTransaction Create(
        InventoryTransactionType type,
        Guid productId,
        Guid warehouseId,
        Guid zoneId,
        decimal quantity,
        string idempotencyKey,
        Guid? lotId = null,
        InventoryBucket? fromBucket = null,
        InventoryBucket? toBucket = null,
        string? refType = null,
        Guid? refId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new InventoryTransaction
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            ProductId = productId,
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            LotId = lotId,
            Quantity = quantity,
            FromBucket = fromBucket,
            ToBucket = toBucket,
            RefType = refType,
            RefId = refId,
            IdempotencyKey = idempotencyKey.Trim(),
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
