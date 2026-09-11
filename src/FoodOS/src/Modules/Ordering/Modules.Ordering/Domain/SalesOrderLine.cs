using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class SalesOrderLine : BaseEntity<Guid>
{
    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Zone { get; private set; } = default!;
    public decimal OrderedQty { get; private set; }
    public decimal ReservedQty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public Guid? ReservationId { get; private set; }

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
}
