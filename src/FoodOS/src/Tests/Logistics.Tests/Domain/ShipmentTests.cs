using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Domain;

namespace Logistics.Tests.Domain;

public sealed class ShipmentTests
{
    [Fact]
    public void LoadThenDepartThenPod_Should_FollowStatusMachine()
    {
        var storeId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        var orderLineId = Guid.CreateVersion7();
        var lotId = Guid.CreateVersion7();
        var shipment = Shipment.Create(
            "SH20260911R0101",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 11),
            Guid.CreateVersion7(),
            Guid.CreateVersion7());
        var stop = shipment.AddStop(storeId, 1, "05:00-08:00");
        var line = shipment.AddLine(orderId, storeId);
        line.AddLot(orderLineId, Guid.CreateVersion7(), "Ambient", lotId, "LOT-A", 6m);

        var wrongLoad = Should.Throw<CustomException>(() => shipment.Load([Guid.CreateVersion7()]));
        wrongLoad.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);

        shipment.Load([orderId]);
        shipment.Status.ShouldBe(ShipmentStatus.Loading);

        var earlyPod = Should.Throw<CustomException>(() =>
            shipment.ConfirmStop(stop.Id, "[]", [], "Chef", null));
        earlyPod.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);

        shipment.Depart();
        shipment.Status.ShouldBe(ShipmentStatus.Departed);

        shipment.ConfirmStop(stop.Id, """[{"lotId":1}]""", [Guid.CreateVersion7()], "Chef", "42.3,-71.0");
        stop.Status.ShouldBe(StopStatus.Delivered);
        stop.ProofOfDelivery.ShouldNotBeNull();
        shipment.Status.ShouldBe(ShipmentStatus.Completed);
    }

    [Fact]
    public void RecordReturn_Should_StayOnSameShipment()
    {
        var shipment = Shipment.Create(
            "SH20260911R0102",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 11),
            Guid.CreateVersion7(),
            Guid.CreateVersion7());
        var ret = shipment.RecordReturn(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            2m,
            "partial-reject");

        shipment.Returns.ShouldHaveSingleItem().Id.ShouldBe(ret.Id);
        ret.Reason.ShouldBe("partial-reject");
        ret.Quantity.ShouldBe(2m);
    }
}
