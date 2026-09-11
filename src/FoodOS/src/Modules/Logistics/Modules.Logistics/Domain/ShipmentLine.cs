using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class ShipmentLine : BaseEntity<Guid>
{
    private readonly List<ShipmentLineLot> _lots = [];

    public Guid ShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid? ToteId { get; private set; }

    public IReadOnlyList<ShipmentLineLot> Lots => _lots;

    private ShipmentLine() { }

    internal static ShipmentLine Create(Guid shipmentId, Guid orderId, Guid storeId, Guid? toteId = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(orderId));
        }

        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("StoreId is required.", nameof(storeId));
        }

        return new ShipmentLine
        {
            Id = Guid.CreateVersion7(),
            ShipmentId = shipmentId,
            OrderId = orderId,
            StoreId = storeId,
            ToteId = toteId
        };
    }

    internal ShipmentLineLot AddLot(
        Guid orderLineId,
        Guid productId,
        string zone,
        Guid lotId,
        string lotNo,
        decimal quantity)
    {
        var lot = ShipmentLineLot.Create(Id, orderLineId, productId, zone, lotId, lotNo, quantity);
        _lots.Add(lot);
        return lot;
    }
}
