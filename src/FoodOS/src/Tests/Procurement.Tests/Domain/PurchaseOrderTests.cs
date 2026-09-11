using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Domain;

namespace Procurement.Tests.Domain;

public sealed class PurchaseOrderTests
{
    [Fact]
    public void Send_Should_TransitionDraftToSent()
    {
        var po = CreateDraft();

        po.Send();

        po.Status.ShouldBe(PurchaseOrderStatus.Sent);
    }

    [Fact]
    public void Send_Should_Reject_When_NotDraft()
    {
        var po = CreateDraft();
        po.Send();

        var ex = Should.Throw<CustomException>(() => po.Send());
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void Appoint_Should_SendDraftAndMoveToReceiving()
    {
        var po = CreateDraft();

        var appointment = po.Appoint("DOCK-1", "AB-1234");

        po.Status.ShouldBe(PurchaseOrderStatus.Receiving);
        po.Appointment.ShouldNotBeNull();
        appointment.DockSlot.ShouldBe("DOCK-1");
        appointment.VehicleNo.ShouldBe("AB-1234");
    }

    [Fact]
    public void Appoint_Should_Reject_When_AlreadyAppointed()
    {
        var po = CreateDraft();
        po.Appoint("DOCK-1", null);

        var ex = Should.Throw<CustomException>(() => po.Appoint("DOCK-2", null));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void RecordQualityCheck_Should_Reject_When_NotReceiving()
    {
        var po = CreateDraft();
        var lineId = po.Lines[0].Id;

        var ex = Should.Throw<CustomException>(() =>
            po.RecordQualityCheck(
                lineId,
                Guid.CreateVersion7(),
                QualityCheckResult.Pass,
                sampleQty: 1,
                quantity: 5,
                lotNo: "LOT-1",
                lotId: Guid.CreateVersion7(),
                note: null,
                photoFileIds: null));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void RecordQualityCheck_Pass_Should_IncreaseReceivedQty()
    {
        var po = CreateReceiving();
        var line = po.Lines[0];

        var check = po.RecordQualityCheck(
            line.Id,
            Guid.CreateVersion7(),
            QualityCheckResult.Pass,
            sampleQty: 1,
            quantity: 4,
            lotNo: "LOT-PASS",
            lotId: Guid.CreateVersion7(),
            note: "ok",
            photoFileIds: null);

        check.Result.ShouldBe(QualityCheckResult.Pass);
        line.ReceivedQty.ShouldBe(4m);
        line.RejectedQty.ShouldBe(0m);
        po.ReceiveRecords.Count.ShouldBe(1);
    }

    [Fact]
    public void RecordQualityCheck_Fail_Should_IncreaseRejectedQty()
    {
        var po = CreateReceiving();
        var line = po.Lines[0];

        var check = po.RecordQualityCheck(
            line.Id,
            Guid.CreateVersion7(),
            QualityCheckResult.Fail,
            sampleQty: 1,
            quantity: 4,
            lotNo: "LOT-FAIL",
            lotId: Guid.CreateVersion7(),
            note: "temp",
            photoFileIds: null);

        check.Result.ShouldBe(QualityCheckResult.Fail);
        line.ReceivedQty.ShouldBe(0m);
        line.RejectedQty.ShouldBe(4m);
    }

    private static PurchaseOrder CreateDraft()
        => PurchaseOrder.Create(
            "PO202609110001",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow.AddDays(1),
            [(Guid.CreateVersion7(), "Ambient", 10m)]);

    private static PurchaseOrder CreateReceiving()
    {
        var po = CreateDraft();
        po.Appoint("DOCK-1", null);
        return po;
    }
}
