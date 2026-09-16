using FSH.Framework.Core.Exceptions;
using FSH.Modules.Warehouse.Domain;

namespace Warehouse.Tests.Domain;

public sealed class WaveTests
{
    [Fact]
    public void Assignment_Should_BeExplicit_Idempotent_And_Immutable()
    {
        var wave = Wave.Create("ASSIGN", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Ambient", new DateOnly(2026, 9, 16));
        wave.AssignedPickerUserId.ShouldBeNull();
        Should.Throw<ArgumentException>(() => wave.AssignPicker(Guid.Empty));
        Guid picker = Guid.NewGuid();
        wave.AssignPicker(picker);
        wave.AssignPicker(picker);
        wave.AssignedPickerUserId.ShouldBe(picker);
        Should.Throw<CustomException>(() => wave.AssignPicker(Guid.NewGuid())).StatusCode
            .ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void CompletedLegacyWave_Should_NotBeAssigned()
    {
        var wave = Wave.Create("LEGACY", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Ambient", new DateOnly(2026, 9, 16));
        var task = wave.AddTask(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "Ambient", Guid.NewGuid(), 1m);
        task.MarkShorted(1m);
        wave.CompleteIfDone();
        Should.Throw<CustomException>(() => wave.AssignPicker(Guid.NewGuid())).StatusCode
            .ShouldBe(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public void ReleaseThenPick_Should_AssignLotAndRejectWrongScan()
    {
        var wave = Wave.Create(
            "WVDC1AMBIENT2026091101",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ambient",
            new DateOnly(2026, 9, 11));
        var allocatedLot = Guid.CreateVersion7();
        var task = wave.AddTask(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ambient",
            Guid.CreateVersion7(),
            6m);
        task.BindAllocation(allocatedLot, "LOT-A", 6m, 0m);

        wave.Release();
        wave.MarkPicking();
        wave.Status.ShouldBe(WaveStatus.Picking);
        task.LotNo.ShouldBe("LOT-A");

        var wrong = Should.Throw<CustomException>(() => task.Confirm(Guid.CreateVersion7(), Guid.CreateVersion7()));
        wrong.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        task.Status.ShouldBe(PickTaskStatus.Pending);

        task.Confirm(allocatedLot, Guid.CreateVersion7());
        task.Status.ShouldBe(PickTaskStatus.Picked);
        wave.CompleteIfDone();
        wave.Status.ShouldBe(WaveStatus.Completed);
    }

    [Fact]
    public void MarkShorted_Should_CompleteWithoutLot()
    {
        var wave = Wave.Create(
            "WVDC1AMBIENT2026091102",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ambient",
            new DateOnly(2026, 9, 11));
        var task = wave.AddTask(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ambient",
            Guid.CreateVersion7(),
            4m);

        task.MarkShorted(4m);

        task.Status.ShouldBe(PickTaskStatus.Shorted);
        task.LotId.ShouldBeNull();
        task.IsComplete.ShouldBeTrue();
        wave.Release();
        wave.MarkPicking();
        wave.CompleteIfDone();
        wave.Status.ShouldBe(WaveStatus.Completed);
    }
}
