using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class LotBalanceTests
{
    [Fact]
    public void Isolate_Should_ReduceAvailable_WithoutChangingOnHand()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(10);

        balance.Isolate(4);

        balance.OnHand.ShouldBe(10m);
        balance.Isolated.ShouldBe(4m);
        balance.Available.ShouldBe(6m);
        balance.IsFullyIsolated.ShouldBeFalse();
    }

    [Fact]
    public void Isolate_Should_Throw_When_QtyExceedsAvailable()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(5);
        balance.Reserve(3);

        Should.Throw<InvalidOperationException>(() => balance.Isolate(3));
        balance.Isolated.ShouldBe(0m);
        balance.Available.ShouldBe(2m);
    }

    [Fact]
    public void Isolate_Should_MarkFullyIsolated_When_AllOnHandFrozen()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(5);
        balance.Isolate(5);

        balance.IsFullyIsolated.ShouldBeTrue();
        balance.Available.ShouldBe(0m);
    }

    [Fact]
    public void ReceiveIsolated_Should_NotChangeAvailable_And_MarkFullyIsolated()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        balance.ReceiveIsolated(8);

        balance.OnHand.ShouldBe(8m);
        balance.Isolated.ShouldBe(8m);
        balance.Available.ShouldBe(0m);
        balance.IsFullyIsolated.ShouldBeTrue();
    }

    [Fact]
    public void Unreserve_Should_RestoreAvailable()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(8);
        balance.Reserve(3);
        balance.Unreserve(2);

        balance.Reserved.ShouldBe(1m);
        balance.Available.ShouldBe(7m);
    }

    [Fact]
    public void AllocateFromAvailable_Should_IncreaseAllocated_WithoutLotReserved()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(10);

        balance.AllocateFromAvailable(4);

        balance.Reserved.ShouldBe(0m);
        balance.Allocated.ShouldBe(4m);
        balance.Available.ShouldBe(6m);
    }

    [Fact]
    public void AllocateFromAvailable_Should_Throw_When_ExceedsAvailable()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(5);
        balance.Isolate(3);

        Should.Throw<InvalidOperationException>(() => balance.AllocateFromAvailable(3));
        balance.Allocated.ShouldBe(0m);
    }

    [Fact]
    public void ShipThenDeliver_Should_LeaveOnHandUnchangedAfterPick()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(10);
        balance.AllocateFromAvailable(6);
        balance.Pick(6);

        balance.Ship(6);
        balance.Picked.ShouldBe(0m);
        balance.InTransit.ShouldBe(6m);
        balance.OnHand.ShouldBe(4m);

        balance.Deliver(4);
        balance.ReturnToWarehouse(2);

        balance.InTransit.ShouldBe(0m);
        balance.OnHand.ShouldBe(6m);
    }

    [Fact]
    public void Deliver_Should_Throw_When_ExceedsInTransit()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(3);
        balance.AllocateFromAvailable(3);
        balance.Pick(3);
        balance.Ship(3);

        Should.Throw<InvalidOperationException>(() => balance.Deliver(4));
        balance.InTransit.ShouldBe(3m);
    }

    [Fact]
    public void Shrink_Should_WriteOffIsolatedFirst_ThenAvailable()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(10);
        balance.Isolate(4);

        balance.Shrink(4);
        balance.Isolated.ShouldBe(0m);
        balance.OnHand.ShouldBe(6m);
        balance.Available.ShouldBe(6m);

        balance.Shrink(2);
        balance.OnHand.ShouldBe(4m);
        balance.Available.ShouldBe(4m);
    }

    [Fact]
    public void Shrink_Should_Throw_When_ExceedsAvailable()
    {
        var balance = LotBalance.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        balance.Receive(5);
        balance.Reserve(3);

        Should.Throw<InvalidOperationException>(() => balance.Shrink(3));
        balance.OnHand.ShouldBe(5m);
    }
}
