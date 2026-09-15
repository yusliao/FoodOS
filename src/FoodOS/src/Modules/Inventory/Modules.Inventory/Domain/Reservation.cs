using FSH.Framework.Core.Domain;

namespace FSH.Modules.Inventory.Domain;

/// <summary>
/// SKU-level hold at warehouse × zone. Lots are not locked until wave allocation (FEFO).
/// </summary>
public sealed class Reservation : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? OrderLineId { get; private set; }
    public decimal Quantity { get; private set; }
    public bool Released { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    private Reservation() { }

    public static Reservation Create(
        Guid warehouseId,
        Guid zoneId,
        Guid productId,
        Guid orderId,
        decimal quantity,
        Guid? orderLineId = null)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        if (zoneId == Guid.Empty)
        {
            throw new ArgumentException("ZoneId is required.", nameof(zoneId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(orderId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new Reservation
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            ProductId = productId,
            OrderId = orderId,
            OrderLineId = orderLineId,
            Quantity = quantity,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Release()
    {
        if (Released)
        {
            return;
        }

        Released = true;
        ReleasedAt = DateTimeOffset.UtcNow;
    }
}
