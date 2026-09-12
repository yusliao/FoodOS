using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class SalesOrderLine : BaseEntity<Guid>
{
    private readonly List<SalesOrderLineLot> _lots = [];

    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Zone { get; private set; } = default!;
    public decimal OrderedQty { get; private set; }
    public decimal ReservedQty { get; private set; }
    public decimal DeliveredQty { get; private set; }
    public decimal ReturnedQty { get; private set; }
    public decimal ShortageQty { get; private set; }
    public string? ShortageReason { get; private set; }
    public string? VarianceReason { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public Guid? ReservationId { get; private set; }

    public IReadOnlyList<SalesOrderLineLot> Lots => _lots;

    private SalesOrderLine() { }

    internal static SalesOrderLine Create(
        Guid salesOrderId,
        Guid productId,
        string zone,
        decimal orderedQty,
        decimal unitPrice,
        string currency)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(zone);

        if (orderedQty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderedQty), "Quantity must be positive.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new SalesOrderLine
        {
            Id = Guid.CreateVersion7(),
            SalesOrderId = salesOrderId,
            ProductId = productId,
            Zone = zone.Trim(),
            OrderedQty = orderedQty,
            UnitPrice = unitPrice,
            Currency = currency.Trim().ToUpperInvariant()
        };
    }

    internal void BindReservation(Guid reservationId, decimal reservedQty)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("ReservationId is required.", nameof(reservationId));
        }

        if (reservedQty < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedQty), "Reserved quantity cannot be negative.");
        }

        ReservationId = reservationId;
        ReservedQty = reservedQty;
    }

    internal void ClearReservation()
    {
        ReservationId = null;
        ReservedQty = 0;
    }

    internal void BindShipmentLots(IReadOnlyList<(Guid LotId, string LotNo, decimal Qty)> lots)
    {
        ArgumentNullException.ThrowIfNull(lots);
        if (_lots.Count > 0)
        {
            return;
        }

        foreach (var (lotId, lotNo, qty) in lots)
        {
            _lots.Add(SalesOrderLineLot.Create(Id, lotId, lotNo, qty));
        }
    }

    internal void RecordReceipt(Guid lotId, decimal deliveredQty, decimal returnedQty, string? varianceReason)
    {
        var lot = _lots.Find(l => l.LotId == lotId)
            ?? throw new InvalidOperationException($"Lot {lotId} was not shipped on this order line.");

        lot.RecordReceipt(deliveredQty, returnedQty);
        DeliveredQty = _lots.Sum(l => l.DeliveredQty);
        ReturnedQty = _lots.Sum(l => l.ReturnedQty);
        VarianceReason = string.IsNullOrWhiteSpace(varianceReason) ? VarianceReason : varianceReason.Trim();
    }

    internal void RecordShortage(decimal shortageQty, string reason)
    {
        if (shortageQty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shortageQty), "Shortage quantity must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ShortageQty = shortageQty;
        ShortageReason = reason.Trim();
    }
}
