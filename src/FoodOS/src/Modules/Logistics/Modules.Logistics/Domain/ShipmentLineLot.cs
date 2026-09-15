using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class ShipmentLineLot : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid ShipmentLineId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Zone { get; private set; } = default!;
    public Guid LotId { get; private set; }
    public string LotNo { get; private set; } = default!;
    public decimal Quantity { get; private set; }

    private ShipmentLineLot() { }

    internal static ShipmentLineLot Create(
        Guid shipmentLineId,
        Guid orderLineId,
        Guid productId,
        string zone,
        Guid lotId,
        string lotNo,
        decimal quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        ArgumentException.ThrowIfNullOrWhiteSpace(lotNo);
        if (orderLineId == Guid.Empty || productId == Guid.Empty || lotId == Guid.Empty)
        {
            throw new ArgumentException("Order line, product and lot are required.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new ShipmentLineLot
        {
            Id = Guid.CreateVersion7(),
            ShipmentLineId = shipmentLineId,
            OrderLineId = orderLineId,
            ProductId = productId,
            Zone = zone.Trim(),
            LotId = lotId,
            LotNo = lotNo.Trim().ToUpperInvariant(),
            Quantity = quantity
        };
    }
}
