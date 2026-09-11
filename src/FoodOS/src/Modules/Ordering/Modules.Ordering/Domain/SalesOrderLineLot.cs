using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

/// <summary>
/// Lot snapshot bound at dispatch so Shop can scan the same batch after POD.
/// </summary>
public sealed class SalesOrderLineLot : BaseEntity<Guid>
{
    public Guid SalesOrderLineId { get; private set; }
    public Guid LotId { get; private set; }
    public string LotNo { get; private set; } = default!;
    public decimal ShippedQty { get; private set; }
    public decimal DeliveredQty { get; private set; }
    public decimal ReturnedQty { get; private set; }

    private SalesOrderLineLot() { }

    internal static SalesOrderLineLot Create(Guid salesOrderLineId, Guid lotId, string lotNo, decimal shippedQty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotNo);
        if (lotId == Guid.Empty)
        {
            throw new ArgumentException("LotId is required.", nameof(lotId));
        }

        if (shippedQty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shippedQty), "Shipped quantity must be positive.");
        }

        return new SalesOrderLineLot
        {
            Id = Guid.CreateVersion7(),
            SalesOrderLineId = salesOrderLineId,
            LotId = lotId,
            LotNo = lotNo.Trim().ToUpperInvariant(),
            ShippedQty = shippedQty
        };
    }

    internal void RecordReceipt(decimal deliveredQty, decimal returnedQty)
    {
        if (deliveredQty < 0 || returnedQty < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveredQty), "Receipt quantities cannot be negative.");
        }

        if (deliveredQty + returnedQty != ShippedQty)
        {
            throw new InvalidOperationException("Delivered plus returned must equal shipped quantity.");
        }

        DeliveredQty = deliveredQty;
        ReturnedQty = returnedQty;
    }
}
