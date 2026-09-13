using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Dtos;

namespace Inventory.Tests.Domain;

public sealed class OperatingClockEvaluatorTests
{
    [Fact]
    public void IsPastLocalTime_Should_BeFalse_BeforeReconcile()
    {
        var clock = new OperatingClockDto("16:00", "22:00", "05:00", "08:00", "10:00", "UTC");
        var now = new DateTimeOffset(2026, 9, 13, 9, 59, 0, TimeSpan.Zero);

        OperatingClockEvaluator.IsPastLocalTime(clock, clock.ReconcileLocal, now).ShouldBeFalse();
        OperatingClockEvaluator.LocalDate(clock, now).ShouldBe(new DateOnly(2026, 9, 13));
    }

    [Fact]
    public void IsPastLocalTime_Should_BeTrue_AtReconcile()
    {
        var clock = new OperatingClockDto("16:00", "22:00", "05:00", "08:00", "10:00", "UTC");
        var now = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

        OperatingClockEvaluator.IsPastLocalTime(clock, clock.ReconcileLocal, now).ShouldBeTrue();
    }
}
