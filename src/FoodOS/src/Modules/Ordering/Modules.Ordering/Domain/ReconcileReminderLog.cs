using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

/// <summary>One row per warehouse local date so the reconcile reminder job does not re-notify.</summary>
public sealed class ReconcileReminderLog : BaseEntity<Guid>
{
    public Guid WarehouseId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public int OpenOrderCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ReconcileReminderLog() { }

    public static ReconcileReminderLog Record(Guid warehouseId, DateOnly localDate, int openOrderCount)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(openOrderCount);

        return new ReconcileReminderLog
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            LocalDate = localDate,
            OpenOrderCount = openOrderCount,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
