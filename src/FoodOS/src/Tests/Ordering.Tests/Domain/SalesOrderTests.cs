using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Domain.Events;

namespace Ordering.Tests.Domain;

public sealed class SalesOrderTests
{
    [Fact]
    public void Place_Should_TransitionDraftToReserved_When_LinesAreReserved()
    {
        var order = CreateDraft();
        foreach (var line in order.Lines)
        {
            line.BindReservation(Guid.CreateVersion7(), line.OrderedQty);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        order.Place(now);

        order.Status.ShouldBe(SalesOrderStatus.Reserved);
        order.PlacedAt.ShouldBe(now);
        order.DomainEvents.OfType<SalesOrderPlacedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Place_Should_Reject_When_ReservationMissing()
    {
        var order = CreateDraft();

        var ex = Should.Throw<CustomException>(() => order.Place(DateTimeOffset.UtcNow));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void Amend_Should_Reject_AfterCutoff()
    {
        var order = CreateReserved(cutoffAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var ex = Should.Throw<CustomException>(() => order.BeginAmend(DateTimeOffset.UtcNow));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void Cancel_Should_ReleaseAndMarkCancelled_BeforeCutoff()
    {
        var order = CreateReserved(cutoffAt: DateTimeOffset.UtcNow.AddHours(2));

        order.Cancel(DateTimeOffset.UtcNow);

        order.Status.ShouldBe(SalesOrderStatus.Cancelled);
        order.Lines.ShouldAllBe(l => l.ReservationId == null && l.ReservedQty == 0);
        order.DomainEvents.OfType<SalesOrderCancelledDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Cancel_Should_Reject_AfterCutoff()
    {
        var order = CreateReserved(cutoffAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var ex = Should.Throw<CustomException>(() => order.Cancel(DateTimeOffset.UtcNow));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void Transitions_Should_AllowFullFulfillmentChain()
    {
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Draft, SalesOrderStatus.Reserved).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Reserved, SalesOrderStatus.Planned).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Planned, SalesOrderStatus.Picking).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Picking, SalesOrderStatus.Packed).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Packed, SalesOrderStatus.InTransit).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.InTransit, SalesOrderStatus.Received).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Received, SalesOrderStatus.Reconciled).ShouldBeTrue();
        SalesOrderTransitions.CanTransition(SalesOrderStatus.Draft, SalesOrderStatus.Picking).ShouldBeFalse();
    }

    private static SalesOrder CreateDraft()
        => SalesOrder.CreateDraft(
            "SO202609110001",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 11),
            DateTimeOffset.UtcNow.AddHours(2),
            [(Guid.CreateVersion7(), "Ambient", 4m, 12.5m, "USD")]);

    private static SalesOrder CreateReserved(DateTimeOffset cutoffAt)
    {
        var order = SalesOrder.CreateDraft(
            "SO202609110002",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 11),
            cutoffAt,
            [(Guid.CreateVersion7(), "Ambient", 4m, 12.5m, "USD")]);
        foreach (var line in order.Lines)
        {
            line.BindReservation(Guid.CreateVersion7(), line.OrderedQty);
        }

        order.Place(DateTimeOffset.UtcNow.AddHours(-1));
        order.ClearDomainEvents();
        return order;
    }
}
