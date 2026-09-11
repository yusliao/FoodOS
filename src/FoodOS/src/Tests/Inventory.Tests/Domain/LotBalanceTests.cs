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
}
