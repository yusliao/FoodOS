using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class FefoAllocatorTests
{
    [Fact]
    public void Take_Should_SkipIsolatedLots_And_PickEarliestExpiry()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var zoneId = Guid.CreateVersion7();
        DateOnly today = new(2026, 9, 11);

        var early = Lot.Create("L-EARLY", productId, today.AddDays(5));
        var late = Lot.Create("L-LATE", productId, today.AddDays(20));
        var isolated = Lot.Create("L-ISO", productId, today.AddDays(1));
        isolated.Isolate();

        var earlyBal = LotBalance.Create(warehouseId, zoneId, early.Id, productId);
        earlyBal.Receive(4);
        var lateBal = LotBalance.Create(warehouseId, zoneId, late.Id, productId);
        lateBal.Receive(10);
        var isoBal = LotBalance.Create(warehouseId, zoneId, isolated.Id, productId);
        isoBal.ReceiveIsolated(8);

        var slices = FefoAllocator.Take(
            [
                new FefoAllocator.Candidate(isolated, isoBal),
                new FefoAllocator.Candidate(late, lateBal),
                new FefoAllocator.Candidate(early, earlyBal)
            ],
            7m,
            today);

        slices.Count.ShouldBe(2);
        slices[0].Lot.LotNo.ShouldBe("L-EARLY");
        slices[0].Quantity.ShouldBe(4m);
        slices[1].Lot.LotNo.ShouldBe("L-LATE");
        slices[1].Quantity.ShouldBe(3m);
        slices.ShouldNotContain(s => s.Lot.Status == LotStatus.Isolated);
    }

    [Fact]
    public void Take_Should_ReturnEmpty_When_OnlyIsolatedStockExists()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var zoneId = Guid.CreateVersion7();
        DateOnly today = new(2026, 9, 11);

        var isolated = Lot.Create("L-ISO", productId, today.AddDays(10));
        isolated.Isolate();
        var balance = LotBalance.Create(warehouseId, zoneId, isolated.Id, productId);
        balance.ReceiveIsolated(10);

        var slices = FefoAllocator.Take(
            [new FefoAllocator.Candidate(isolated, balance)],
            5m,
            today);

        slices.ShouldBeEmpty();
    }
}
