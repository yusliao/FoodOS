using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

/// <summary>One row per warehouse local date and kind so load/POD reminder jobs do not re-notify.</summary>
public sealed class DispatchReminderLog : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public const string LoadKind = "Load";
    public const string PodKind = "Pod";

    public Guid WarehouseId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public string Kind { get; private set; } = default!;
    public int OpenCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DispatchReminderLog() { }

    public static DispatchReminderLog Record(Guid warehouseId, DateOnly localDate, string kind, int openCount)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (kind is not LoadKind and not PodKind)
        {
            throw new ArgumentException("Kind must be Load or Pod.", nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(openCount);

        return new DispatchReminderLog
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            LocalDate = localDate,
            Kind = kind,
            OpenCount = openCount,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
