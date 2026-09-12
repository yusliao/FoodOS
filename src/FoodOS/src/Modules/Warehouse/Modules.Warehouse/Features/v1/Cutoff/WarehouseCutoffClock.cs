using System.Globalization;
using FSH.Modules.Inventory.Contracts.Dtos;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff;

internal static class WarehouseCutoffClock
{
    public static DateOnly LocalDate(OperatingClockDto clock, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    public static bool IsPastCutoff(OperatingClockDto clock, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        TimeOnly cutoffLocal = TimeOnly.ParseExact(clock.CutoffLocal, "HH:mm", CultureInfo.InvariantCulture);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        DateOnly today = DateOnly.FromDateTime(localNow.DateTime);
        var localCutoff = today.ToDateTime(cutoffLocal, DateTimeKind.Unspecified);
        TimeSpan offset = tz.GetUtcOffset(localCutoff);
        var todayCutoff = new DateTimeOffset(localCutoff, offset);
        return localNow >= todayCutoff;
    }
}
