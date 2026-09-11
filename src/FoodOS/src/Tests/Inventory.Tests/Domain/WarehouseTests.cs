using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class WarehouseTests
{
    [Fact]
    public void Create_Should_SeedThreeTemperatureZonesAndUsdTrialClock()
    {
        var warehouse = Warehouse.Create("dc1", "Pilot DC", "Boston");

        warehouse.Code.ShouldBe("DC1");
        warehouse.Zones.Count.ShouldBe(3);
        warehouse.Clock.TimeZoneId.ShouldBe("America/New_York");
        warehouse.Clock.CutoffLocal.ShouldBe(new TimeOnly(16, 0));
    }
}
