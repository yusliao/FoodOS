using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class OperatingCutoffTests
{
    [Fact]
    public void Resolve_Should_UseToday_When_BeforeCutoff()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 14, 0, 0, TimeSpan.Zero);

        var (businessDate, cutoffAt) = OperatingCutoff.Resolve("UTC", new TimeOnly(16, 0), utcNow);

        businessDate.ShouldBe(new DateOnly(2026, 9, 11));
        cutoffAt.ShouldBe(new DateTimeOffset(2026, 9, 11, 16, 0, 0, TimeSpan.Zero));
        OperatingCutoff.IsPastCutoff(cutoffAt, utcNow).ShouldBeFalse();
    }

    [Fact]
    public void Resolve_Should_RollToNextDay_When_AfterCutoff()
    {
        var utcNow = new DateTimeOffset(2026, 9, 11, 17, 0, 0, TimeSpan.Zero);

        var (businessDate, cutoffAt) = OperatingCutoff.Resolve("UTC", new TimeOnly(16, 0), utcNow);

        businessDate.ShouldBe(new DateOnly(2026, 9, 12));
        cutoffAt.ShouldBe(new DateTimeOffset(2026, 9, 12, 16, 0, 0, TimeSpan.Zero));
        OperatingCutoff.IsPastCutoff(cutoffAt, utcNow).ShouldBeFalse();
    }
}
