using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class AvailableQtyCalculatorTests
{
    [Fact]
    public void Compute_Should_ExcludeIsolatedAndExpiredLots()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var zoneId = Guid.CreateVersion7();
        DateOnly today = new(2026, 9, 11);

        var active = Lot.Create("L-ACTIVE", productId, today.AddDays(10));
        var expired = Lot.Create("L-OLD", productId, today.AddDays(-1));
        var isolatedLot = Lot.Create("L-ISO", productId, today.AddDays(10));
        isolatedLot.Isolate();

        var activeBalance = LotBalance.Create(warehouseId, zoneId, active.Id, productId);
        activeBalance.Receive(10);
        var expiredBalance = LotBalance.Create(warehouseId, zoneId, expired.Id, productId);
        expiredBalance.Receive(5);
        var isolatedBalance = LotBalance.Create(warehouseId, zoneId, isolatedLot.Id, productId);
        isolatedBalance.Receive(8);

        var lots = new Dictionary<Guid, Lot>
        {
            [active.Id] = active,
            [expired.Id] = expired,
            [isolatedLot.Id] = isolatedLot
        };

        decimal available = AvailableQtyCalculator.Compute(
            [activeBalance, expiredBalance, isolatedBalance],
            lots,
            today,
            minRemainingDaysOnShip: 0);

        available.ShouldBe(10m);
    }

    [Fact]
    public void Compute_Should_HonorMinRemainingDaysOnShip()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var zoneId = Guid.CreateVersion7();
        DateOnly today = new(2026, 9, 11);

        var shortLife = Lot.Create("L-SHORT", productId, today.AddDays(2));
        var balance = LotBalance.Create(warehouseId, zoneId, shortLife.Id, productId);
        balance.Receive(4);

        decimal available = AvailableQtyCalculator.Compute(
            [balance],
            new Dictionary<Guid, Lot> { [shortLife.Id] = shortLife },
            today,
            minRemainingDaysOnShip: 5);

        available.ShouldBe(0m);
    }
}
