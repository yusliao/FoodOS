using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Procurement.Domain;

public sealed class PurchaseOrder : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    private readonly List<PurchaseOrderLine> _lines = [];
    private readonly List<QualityCheck> _qualityChecks = [];
    private readonly List<ReceiveRecord> _receiveRecords = [];

    public string Number { get; private set; } = default!;
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public DateTimeOffset ExpectedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public InboundAppointment? Appointment { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;
    public IReadOnlyList<QualityCheck> QualityChecks => _qualityChecks;
    public IReadOnlyList<ReceiveRecord> ReceiveRecords => _receiveRecords;

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        string number,
        Guid supplierId,
        Guid warehouseId,
        DateTimeOffset expectedAt,
        IReadOnlyList<(Guid ProductId, string Zone, decimal Quantity)> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(lines);
        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("SupplierId is required.", nameof(supplierId));
        }

        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        if (lines.Count == 0)
        {
            throw new CustomException(
                "A purchase order must contain at least one line.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var po = new PurchaseOrder
        {
            Id = Guid.CreateVersion7(),
            Number = number.Trim().ToUpperInvariant(),
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = PurchaseOrderStatus.Draft,
            ExpectedAt = expectedAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var (productId, zone, quantity) in lines)
        {
            po._lines.Add(PurchaseOrderLine.Create(po.Id, productId, zone, quantity));
        }

        return po;
    }

    public void Send()
    {
        if (Status is not PurchaseOrderStatus.Draft)
        {
            throw new CustomException(
                "Only draft purchase orders can be sent.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = PurchaseOrderStatus.Sent;
    }

    public InboundAppointment Appoint(string dockSlot, string? vehicleNo)
    {
        if (Status is PurchaseOrderStatus.Draft)
        {
            Send();
        }

        if (Status is not PurchaseOrderStatus.Sent and not PurchaseOrderStatus.Receiving)
        {
            throw new CustomException(
                "Only sent purchase orders can be appointed for receiving.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (Appointment is not null)
        {
            throw new CustomException(
                "This purchase order already has an inbound appointment.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Appointment = InboundAppointment.Create(Id, dockSlot, vehicleNo);
        Status = PurchaseOrderStatus.Receiving;
        return Appointment;
    }

    public PurchaseOrderLine RequireLine(Guid lineId)
        => _lines.Find(l => l.Id == lineId)
           ?? throw new NotFoundException($"Purchase order line {lineId} was not found.");

    public void EnsureCanRecordQualityCheck()
    {
        if (Status is not PurchaseOrderStatus.Receiving)
        {
            throw new CustomException(
                "Quality checks can only be recorded while the purchase order is receiving.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }
    }

    public QualityCheck RecordQualityCheck(
        Guid lineId,
        Guid inspectorUserId,
        QualityCheckResult result,
        decimal sampleQty,
        decimal quantity,
        string lotNo,
        Guid lotId,
        string? note,
        IReadOnlyList<Guid>? photoFileIds)
    {
        EnsureCanRecordQualityCheck();
        var line = RequireLine(lineId);
        var check = QualityCheck.Create(
            Id,
            lineId,
            inspectorUserId,
            result,
            sampleQty,
            quantity,
            lotNo,
            lotId,
            note,
            photoFileIds);

        if (result == QualityCheckResult.Pass)
        {
            line.AddReceived(quantity);
        }
        else
        {
            line.AddRejected(quantity);
        }

        _qualityChecks.Add(check);
        _receiveRecords.Add(ReceiveRecord.Create(Id, check.Id, lotId, quantity, line.Zone));
        return check;
    }
}
