namespace FSH.Modules.Inventory.Domain;

/// <summary>Configurable warehouse job clock. Times are local to <see cref="TimeZoneId"/>.</summary>
public sealed record OperatingClock
{
    public TimeOnly CutoffLocal { get; init; }
    public TimeOnly LoadLocal { get; init; }
    public TimeOnly DeliverFromLocal { get; init; }
    public TimeOnly DeliverToLocal { get; init; }
    public TimeOnly ReconcileLocal { get; init; }
    public string TimeZoneId { get; init; }

    public OperatingClock(
        TimeOnly cutoffLocal,
        TimeOnly loadLocal,
        TimeOnly deliverFromLocal,
        TimeOnly deliverToLocal,
        TimeOnly reconcileLocal,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (deliverToLocal <= deliverFromLocal)
        {
            throw new ArgumentException("Delivery window end must be after start.", nameof(deliverToLocal));
        }

        CutoffLocal = cutoffLocal;
        LoadLocal = loadLocal;
        DeliverFromLocal = deliverFromLocal;
        DeliverToLocal = deliverToLocal;
        ReconcileLocal = reconcileLocal;
        TimeZoneId = timeZoneId.Trim();
    }

    /// <summary>US East default for the EU/US trial; override per warehouse.</summary>
    public static OperatingClock Default(string timeZoneId = "America/New_York")
        => new(
            new TimeOnly(16, 0),
            new TimeOnly(22, 0),
            new TimeOnly(5, 0),
            new TimeOnly(8, 0),
            new TimeOnly(10, 0),
            timeZoneId);
}
