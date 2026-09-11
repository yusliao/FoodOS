using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class ReservationTests
{
    [Fact]
    public void Create_Should_HoldSkuQuantity_UntilReleased()
    {
        var reservation = Reservation.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            8m,
            Guid.CreateVersion7());

        reservation.Released.ShouldBeFalse();
        reservation.Quantity.ShouldBe(8m);

        reservation.Release();
        reservation.Released.ShouldBeTrue();
        reservation.ReleasedAt.ShouldNotBeNull();

        reservation.Release();
        reservation.Released.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_RejectNonPositiveQuantity()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Reservation.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            0m));
    }
}
