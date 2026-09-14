using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Jobs;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistics.Tests.Domain;

public sealed class DispatchReminderTests
{
    [Fact]
    public void Record_Should_Reject_UnknownKind()
    {
        Should.Throw<ArgumentException>(() =>
            DispatchReminderLog.Record(Guid.CreateVersion7(), new DateOnly(2026, 9, 14), "Depart", 1));
    }

    [Fact]
    public void Record_Should_Store_LoadKind()
    {
        var warehouseId = Guid.CreateVersion7();
        var log = DispatchReminderLog.Record(warehouseId, new DateOnly(2026, 9, 14), DispatchReminderLog.LoadKind, 3);
        log.WarehouseId.ShouldBe(warehouseId);
        log.Kind.ShouldBe(DispatchReminderLog.LoadKind);
        log.OpenCount.ShouldBe(3);
    }

    [Fact]
    public void BusinessDate_Should_Roll_To_Tomorrow_After_Cutoff()
    {
        var clock = new OperatingClockDto("16:00", "22:00", "05:00", "08:00", "10:00", "America/New_York");
        var before = new DateTimeOffset(2026, 9, 14, 19, 0, 0, TimeSpan.Zero); // 15:00 ET
        var after = new DateTimeOffset(2026, 9, 14, 21, 0, 0, TimeSpan.Zero); // 17:00 ET
        DispatchReminderPlanner.BusinessDate(clock, before).ShouldBe(new DateOnly(2026, 9, 14));
        DispatchReminderPlanner.BusinessDate(clock, after).ShouldBe(new DateOnly(2026, 9, 15));
    }

    [Fact]
    public async Task RunAsync_Should_NoOp_InTestingHost()
    {
        var job = new DispatchReminderJob(
            tenantStore: null!,
            scopeFactory: null!,
            environment: new TestingHostEnvironment(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<DispatchReminderJob>.Instance);

        await Should.NotThrowAsync(() => job.RunAsync(CancellationToken.None));
    }

    private sealed class TestingHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Logistics.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
