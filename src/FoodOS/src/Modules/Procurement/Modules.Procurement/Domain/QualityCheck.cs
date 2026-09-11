using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class QualityCheck : BaseEntity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid LineId { get; private set; }
    public Guid InspectorUserId { get; private set; }
    public QualityCheckResult Result { get; private set; }
    public decimal SampleQty { get; private set; }
    public decimal Quantity { get; private set; }
    public string LotNo { get; private set; } = default!;
    public Guid? LotId { get; private set; }
    public string? Note { get; private set; }
    public string PhotoFileIds { get; private set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; private set; }

    private QualityCheck() { }

    internal static QualityCheck Create(
        Guid purchaseOrderId,
        Guid lineId,
        Guid inspectorUserId,
        QualityCheckResult result,
        decimal sampleQty,
        decimal quantity,
        string lotNo,
        Guid? lotId,
        string? note,
        IReadOnlyList<Guid>? photoFileIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lotNo);
        if (inspectorUserId == Guid.Empty)
        {
            throw new ArgumentException("InspectorUserId is required.", nameof(inspectorUserId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (sampleQty < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleQty), "SampleQty cannot be negative.");
        }

        return new QualityCheck
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            LineId = lineId,
            InspectorUserId = inspectorUserId,
            Result = result,
            SampleQty = sampleQty,
            Quantity = quantity,
            LotNo = lotNo.Trim().ToUpperInvariant(),
            LotId = lotId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PhotoFileIds = photoFileIds is { Count: > 0 }
                ? string.Join(',', photoFileIds)
                : string.Empty,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<Guid> PhotoIds()
    {
        if (string.IsNullOrWhiteSpace(PhotoFileIds))
        {
            return [];
        }

        return PhotoFileIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
    }
}
