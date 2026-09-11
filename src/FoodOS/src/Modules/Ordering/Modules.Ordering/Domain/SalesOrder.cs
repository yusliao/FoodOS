using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Domain.Events;

namespace FSH.Modules.Ordering.Domain;

public sealed class SalesOrder : AggregateRoot<Guid>
{
    private readonly List<SalesOrderLine> _lines = [];

    public string Number { get; private set; } = default!;
    public Guid StoreId { get; private set; }
    public Guid CustomerOrgId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset CutoffAt { get; private set; }
    public DateTimeOffset? PlacedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int Revision { get; private set; }

    public IReadOnlyList<SalesOrderLine> Lines => _lines;

    private SalesOrder() { }

    public static SalesOrder CreateDraft(
        string number,
        Guid storeId,
        Guid customerOrgId,
        Guid warehouseId,
        DateOnly businessDate,
        DateTimeOffset cutoffAt,
        IReadOnlyList<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(lines);

        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("StoreId is required.", nameof(storeId));
        }

        if (customerOrgId == Guid.Empty)
        {
            throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        }

        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        if (lines.Count == 0)
        {
            throw new CustomException(
                "An order must contain at least one line.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var order = new SalesOrder
        {
            Id = Guid.CreateVersion7(),
            Number = number.Trim().ToUpperInvariant(),
            StoreId = storeId,
            CustomerOrgId = customerOrgId,
            WarehouseId = warehouseId,
            Status = SalesOrderStatus.Draft,
            BusinessDate = businessDate,
            CutoffAt = cutoffAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var (productId, zone, qty, unitPrice, currency) in lines)
        {
            order._lines.Add(SalesOrderLine.Create(order.Id, productId, zone, qty, unitPrice, currency));
        }

        return order;
    }

    public void Place(DateTimeOffset utcNow)
    {
        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Reserved);
        if (_lines.Exists(l => l.ReservationId is null))
        {
            throw new CustomException(
                "All order lines must be reserved before placing.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = SalesOrderStatus.Reserved;
        PlacedAt = utcNow;
        AddDomainEvent(DomainEvent.Create((id, ts) =>
            new SalesOrderPlacedDomainEvent(Id, Number, StoreId, WarehouseId, id, ts)));
    }

    public void BeginAmend(DateTimeOffset utcNow)
    {
        EnsureBeforeCutoff(utcNow);
        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Reserved);
        Revision++;
    }

    public void ReplaceLines(
        IReadOnlyList<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (Status != SalesOrderStatus.Reserved)
        {
            throw new CustomException(
                "Only reserved orders can be amended.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (lines.Count == 0)
        {
            throw new CustomException(
                "An order must contain at least one line.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        _lines.Clear();
        foreach (var (productId, zone, qty, unitPrice, currency) in lines)
        {
            _lines.Add(SalesOrderLine.Create(Id, productId, zone, qty, unitPrice, currency));
        }
    }

    public void CompleteAmend()
    {
        if (Status != SalesOrderStatus.Reserved)
        {
            throw new CustomException(
                "Only reserved orders can be amended.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (_lines.Exists(l => l.ReservationId is null))
        {
            throw new CustomException(
                "All order lines must be reserved after amend.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        AddDomainEvent(DomainEvent.Create((id, ts) =>
            new SalesOrderAmendedDomainEvent(Id, Revision, id, ts)));
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        EnsureBeforeCutoff(utcNow);
        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Cancelled);
        foreach (var line in _lines)
        {
            line.ClearReservation();
        }

        Status = SalesOrderStatus.Cancelled;
        AddDomainEvent(DomainEvent.Create((id, ts) =>
            new SalesOrderCancelledDomainEvent(Id, id, ts)));
    }

    public void LockForCutoff()
    {
        if (Status == SalesOrderStatus.Planned)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Planned);
        Status = SalesOrderStatus.Planned;
    }

    public void StartPicking()
    {
        if (Status == SalesOrderStatus.Picking)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Picking);
        Status = SalesOrderStatus.Picking;
    }

    public void MarkPacked()
    {
        if (Status == SalesOrderStatus.Packed)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Packed);
        Status = SalesOrderStatus.Packed;
    }

    public void MarkInTransit(IReadOnlyList<(Guid OrderLineId, Guid LotId, string LotNo, decimal Qty)> lots)
    {
        ArgumentNullException.ThrowIfNull(lots);
        if (Status == SalesOrderStatus.InTransit)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.InTransit);
        foreach (var group in lots.GroupBy(l => l.OrderLineId))
        {
            var line = _lines.Find(l => l.Id == group.Key)
                ?? throw new CustomException(
                    $"Order line {group.Key} was not found on this order.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            line.BindShipmentLots(group.Select(x => (x.LotId, x.LotNo, x.Qty)).ToList());
        }

        Status = SalesOrderStatus.InTransit;
    }

    public void MarkReceived(IReadOnlyList<(Guid OrderLineId, Guid LotId, decimal DeliveredQty, decimal ReturnedQty, string? Reason)> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        if (Status == SalesOrderStatus.Received)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Received);
        foreach (var receipt in receipts)
        {
            var line = _lines.Find(l => l.Id == receipt.OrderLineId)
                ?? throw new CustomException(
                    $"Order line {receipt.OrderLineId} was not found on this order.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            try
            {
                line.RecordReceipt(receipt.LotId, receipt.DeliveredQty, receipt.ReturnedQty, receipt.Reason);
            }
            catch (InvalidOperationException ex)
            {
                throw new CustomException(ex.Message, (IEnumerable<string>?)null, HttpStatusCode.BadRequest);
            }
        }

        Status = SalesOrderStatus.Received;
    }

    public void Reconcile()
    {
        if (Status == SalesOrderStatus.Reconciled)
        {
            return;
        }

        SalesOrderTransitions.Ensure(Status, SalesOrderStatus.Reconciled);
        Status = SalesOrderStatus.Reconciled;
    }

    public void FailPlace()
    {
        if (Status != SalesOrderStatus.Draft)
        {
            return;
        }

        Status = SalesOrderStatus.Cancelled;
        foreach (var line in _lines)
        {
            line.ClearReservation();
        }
    }

    private void EnsureBeforeCutoff(DateTimeOffset utcNow)
    {
        if (OperatingCutoff.IsPastCutoff(CutoffAt, utcNow))
        {
            throw new CustomException(
                "Order is locked after cutoff.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }
    }
}
