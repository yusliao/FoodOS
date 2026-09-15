using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class ReturnOnTruck : BaseEntity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Reason { get; private set; } = default!;

    private ReturnOnTruck() { }

    internal static ReturnOnTruck Create(
        Guid shipmentId,
        Guid orderId,
        Guid productId,
        Guid lotId,
        decimal quantity,
        string reason,
        Guid? returnId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (orderId == Guid.Empty || productId == Guid.Empty || lotId == Guid.Empty)
        {
            throw new ArgumentException("Order, product and lot are required.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new ReturnOnTruck
        {
            Id = returnId ?? Guid.CreateVersion7(),
            ShipmentId = shipmentId,
            OrderId = orderId,
            ProductId = productId,
            LotId = lotId,
            Quantity = quantity,
            Reason = reason.Trim()
        };
    }
}
