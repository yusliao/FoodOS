using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Dtos;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff;

internal static class WarehouseCutoffClock
{
    public static DateOnly LocalDate(OperatingClockDto clock, DateTimeOffset utcNow)
        => OperatingClockEvaluator.LocalDate(clock, utcNow);

    public static bool IsPastCutoff(OperatingClockDto clock, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return OperatingClockEvaluator.IsPastLocalTime(clock, clock.CutoffLocal, utcNow);
    }
}
