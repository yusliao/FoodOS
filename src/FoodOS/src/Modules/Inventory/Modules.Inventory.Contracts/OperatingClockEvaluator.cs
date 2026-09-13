using System.Globalization;

namespace FSH.Modules.Inventory.Contracts;

/// <summary>Interprets <see cref="Dtos.OperatingClockDto"/> local times against a UTC instant.</summary>
public static class OperatingClockEvaluator
{
    public static DateOnly LocalDate(Dtos.OperatingClockDto clock, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    public static bool IsPastLocalTime(Dtos.OperatingClockDto clock, string hhmm, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(hhmm);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZoneId);
        TimeOnly localTarget = TimeOnly.ParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        DateOnly today = DateOnly.FromDateTime(localNow.DateTime);
        var localAt = today.ToDateTime(localTarget, DateTimeKind.Unspecified);
        TimeSpan offset = tz.GetUtcOffset(localAt);
        var todayAt = new DateTimeOffset(localAt, offset);
        return localNow >= todayAt;
    }
}
