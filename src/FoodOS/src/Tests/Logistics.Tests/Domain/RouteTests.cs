using FSH.Modules.Logistics.Domain;

namespace Logistics.Tests.Domain;

public sealed class RouteTests
{
    [Fact]
    public void Create_Should_PreserveStoreSequence()
    {
        var warehouseId = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var route = Route.Create(warehouseId, "r01", [first, second]);

        route.Code.ShouldBe("R01");
        route.GetStoreIds().ShouldBe([first, second]);
    }
}
