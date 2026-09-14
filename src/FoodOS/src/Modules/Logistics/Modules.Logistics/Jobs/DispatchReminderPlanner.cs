using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Dtos;

namespace FSH.Modules.Logistics.Jobs;

/// <summary>Maps warehouse clock + UTC now onto the same business date as cutoff.</summary>
public static class DispatchReminderPlanner
{
    public static DateOnly BusinessDate(OperatingClockDto clock, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        DateOnly localToday = OperatingClockEvaluator.LocalDate(clock, utcNow);
        return OperatingClockEvaluator.IsPastLocalTime(clock, clock.CutoffLocal, utcNow)
            ? localToday.AddDays(1)
            : localToday;
    }
}
