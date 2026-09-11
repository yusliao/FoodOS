using FSH.Framework.Core.Domain;

namespace FSH.Modules.Inventory.Domain;

/// <summary>
/// Quantity buckets for one lot in one warehouse zone. Every mutation must also write an <see cref="InventoryTransaction"/>.
/// </summary>
public sealed class LotBalance : BaseEntity<Guid>
{
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public Guid LotId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal OnHand { get; private set; }
    public decimal Reserved { get; private set; }
    public decimal Allocated { get; private set; }
    public decimal Picked { get; private set; }
    public decimal InTransit { get; private set; }
    public decimal Isolated { get; private set; }
    public uint Version { get; private set; }

    private LotBalance() { }

    public static LotBalance Create(Guid warehouseId, Guid zoneId, Guid lotId, Guid productId)
    {
        return new LotBalance
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            LotId = lotId,
            ProductId = productId
        };
    }

    public decimal Available => OnHand - Reserved - Allocated - Isolated;

    public void Receive(decimal qty)
    {
        EnsurePositive(qty);
        OnHand += qty;
        Version++;
    }

    /// <summary>
    /// Inbound that is immediately quarantined. OnHand increases with Isolated so ATP
    /// (<see cref="Available"/>) stays unchanged. Used by Procurement QC fail.
    /// </summary>
    public void ReceiveIsolated(decimal qty)
    {
        EnsurePositive(qty);
        OnHand += qty;
        Isolated += qty;
        Version++;
    }

    public void Isolate(decimal qty)
    {
        EnsurePositive(qty);
        if (Available < qty)
        {
            throw new InvalidOperationException("Insufficient available quantity to isolate.");
        }

        Isolated += qty;
        Version++;
    }

    public void ReleaseIsolate(decimal qty)
    {
        EnsurePositive(qty);
        if (Isolated < qty)
        {
            throw new InvalidOperationException("Insufficient isolated quantity to release.");
        }

        Isolated -= qty;
        Version++;
    }

    public void Reserve(decimal qty)
    {
        EnsurePositive(qty);
        if (Available < qty)
        {
            throw new InvalidOperationException("Insufficient available quantity to reserve.");
        }

        Reserved += qty;
        Version++;
    }

    public void Unreserve(decimal qty)
    {
        EnsurePositive(qty);
        if (Reserved < qty)
        {
            throw new InvalidOperationException("Insufficient reserved quantity to unreserve.");
        }

        Reserved -= qty;
        Version++;
    }

    public bool IsFullyIsolated => Isolated == OnHand && Reserved == 0 && Allocated == 0;

    public void Allocate(decimal qty)
    {
        EnsurePositive(qty);
        if (Reserved < qty)
        {
            throw new InvalidOperationException("Insufficient reserved quantity to allocate.");
        }

        Reserved -= qty;
        Allocated += qty;
        Version++;
    }

    /// <summary>
    /// Wave FEFO: consume open ATP into Allocated. Shop holds live on the SKU
    /// <see cref="Reservation"/> row, not on <see cref="Reserved"/>.
    /// </summary>
    public void AllocateFromAvailable(decimal qty)
    {
        EnsurePositive(qty);
        if (Available < qty)
        {
            throw new InvalidOperationException("Insufficient available quantity to allocate.");
        }

        Allocated += qty;
        Version++;
    }

    public void Pick(decimal qty)
    {
        EnsurePositive(qty);
        if (Allocated < qty || OnHand < qty)
        {
            throw new InvalidOperationException("Insufficient allocated on-hand quantity to pick.");
        }

        Allocated -= qty;
        OnHand -= qty;
        Picked += qty;
        Version++;
    }

    public void Ship(decimal qty)
    {
        EnsurePositive(qty);
        if (Picked < qty)
        {
            throw new InvalidOperationException("Insufficient picked quantity to ship.");
        }

        Picked -= qty;
        InTransit += qty;
        Version++;
    }

    private static void EnsurePositive(decimal qty)
    {
        if (qty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be positive.");
        }
    }
}
