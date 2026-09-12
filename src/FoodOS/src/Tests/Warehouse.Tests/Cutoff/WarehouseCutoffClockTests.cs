using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Warehouse.Features.v1.Cutoff;

namespace Warehouse.Tests.Cutoff;

public sealed class WarehouseCutoffClockTests
{
    [Fact]
    public void IsPastCutoff_Should_BeFalse_BeforeLocalCutoff()
    {
        var clock = new OperatingClockDto("16:00", "22:00", "05:00", "08:00", "10:00", "UTC");
        var now = new DateTimeOffset(2026, 9, 12, 15, 59, 0, TimeSpan.Zero);

        WarehouseCutoffClock.IsPastCutoff(clock, now).ShouldBeFalse();
        WarehouseCutoffClock.LocalDate(clock, now).ShouldBe(new DateOnly(2026, 9, 12));
    }

    [Fact]
    public void IsPastCutoff_Should_BeTrue_AtLocalCutoff()
    {
        var clock = new OperatingClockDto("16:00", "22:00", "05:00", "08:00", "10:00", "UTC");
        var now = new DateTimeOffset(2026, 9, 12, 16, 0, 0, TimeSpan.Zero);

        WarehouseCutoffClock.IsPastCutoff(clock, now).ShouldBeTrue();
    }
}
