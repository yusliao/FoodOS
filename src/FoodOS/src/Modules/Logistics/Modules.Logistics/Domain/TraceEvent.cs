using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class TraceEvent : BaseEntity<Guid>
{
    public Guid? LotId { get; private set; }
    public Guid ProductId { get; private set; }
    public string BizStep { get; private set; } = default!;
    public string Disposition { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public string Uom { get; private set; } = default!;
    public string? SourceLocation { get; private set; }
    public string? DestLocation { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string RefType { get; private set; } = default!;
    public Guid RefId { get; private set; }

    private TraceEvent() { }

    public static TraceEvent Capture(
        Guid productId,
        string bizStep,
        string disposition,
        decimal quantity,
        string uom,
        string actorUserId,
        string refType,
        Guid refId,
        Guid? lotId = null,
        string? sourceLocation = null,
        string? destLocation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bizStep);
        ArgumentException.ThrowIfNullOrWhiteSpace(disposition);
        ArgumentException.ThrowIfNullOrWhiteSpace(uom);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refType);

        return new TraceEvent
        {
            Id = Guid.CreateVersion7(),
            LotId = lotId,
            ProductId = productId,
            BizStep = bizStep.Trim(),
            Disposition = disposition.Trim(),
            Quantity = quantity,
            Uom = uom.Trim(),
            SourceLocation = sourceLocation,
            DestLocation = destLocation,
            ActorUserId = actorUserId,
            OccurredAt = DateTimeOffset.UtcNow,
            RefType = refType.Trim(),
            RefId = refId
        };
    }
}
