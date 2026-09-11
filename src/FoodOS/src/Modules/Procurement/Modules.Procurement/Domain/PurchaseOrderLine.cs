using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class PurchaseOrderLine : BaseEntity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Zone { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public decimal RejectedQty { get; private set; }

    private PurchaseOrderLine() { }

    internal static PurchaseOrderLine Create(Guid purchaseOrderId, Guid productId, string zone, decimal quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new PurchaseOrderLine
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            ProductId = productId,
            Zone = zone.Trim(),
            Quantity = quantity
        };
    }

    internal void AddReceived(decimal qty)
    {
        if (qty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be positive.");
        }

        ReceivedQty += qty;
    }

    internal void AddRejected(decimal qty)
    {
        if (qty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be positive.");
        }

        RejectedQty += qty;
    }
}
