using FSH.Framework.Core.Domain;

namespace FSH.Modules.Inventory.Domain;

public enum DailyPlanStatus
{
    CutOff = 0
}

/// <summary>
/// Snapshot created when a warehouse cutoff is triggered. Existence of a row
/// for (warehouse, business date) means that date is locked.
/// </summary>
public sealed class DailyPlan : AggregateRoot<Guid>
{
    public Guid WarehouseId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset CutoffAt { get; private set; }
    public DailyPlanStatus Status { get; private set; }

    private DailyPlan() { }

    public static DailyPlan Open(Guid warehouseId, DateOnly businessDate, DateTimeOffset cutoffAt)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        return new DailyPlan
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            BusinessDate = businessDate,
            CutoffAt = cutoffAt,
            Status = DailyPlanStatus.CutOff
        };
    }
}
