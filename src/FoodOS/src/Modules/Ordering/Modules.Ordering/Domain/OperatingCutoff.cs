namespace FSH.Modules.Ordering.Domain;

/// <summary>
/// Resolves the next cutoff from a warehouse operating clock. Times are local to the warehouse zone.
/// After cutoff, P0 orders roll to the next business date rather than inserting into today's wave.
/// </summary>
public static class OperatingCutoff
{
    public static (DateOnly BusinessDate, DateTimeOffset CutoffAt) Resolve(
        string timeZoneId,
        TimeOnly cutoffLocal,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        DateOnly today = DateOnly.FromDateTime(localNow.DateTime);

        DateTimeOffset CutoffOn(DateOnly date)
        {
            var local = date.ToDateTime(cutoffLocal, DateTimeKind.Unspecified);
            TimeSpan offset = tz.GetUtcOffset(local);
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        DateTimeOffset todayCutoff = CutoffOn(today);
        if (localNow < todayCutoff)
        {
            return (today, todayCutoff);
        }

        DateOnly tomorrow = today.AddDays(1);
        return (tomorrow, CutoffOn(tomorrow));
    }

    public static bool IsPastCutoff(DateTimeOffset cutoffAt, DateTimeOffset utcNow)
        => utcNow >= cutoffAt;
}
