using FSH.Modules.Warehouse;
using FSH.Modules.Warehouse.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.Storage;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging.Abstractions;

namespace Integration.Tests.Tests.Warehouse;

[Collection(FshCollectionDefinition.Name)]
public sealed class ExternalWmsSchedulingTests
{
    [Fact]
    public async Task Mapping_Should_RetirePersistedCutoffSchedule_WithoutRemovingOtherJobs()
    {
        var sharedStorage = JobStorage.Current;
        using var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        const string otherId = "wms-scheduling-test-unrelated";
        var payload = Job.FromExpression<CutoffJob>(job => job.RunAsync(CancellationToken.None));
        manager.AddOrUpdate("warehouse-cutoff", payload, "* * * * *", new RecurringJobOptions());
        manager.AddOrUpdate(otherId, payload, "0 0 * * *", new RecurringJobOptions());
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddApiVersioning();
            builder.Services.AddSingleton<IRecurringJobManager>(manager);
            await using var app = builder.Build();
            new WarehouseModule().MapEndpoints(app);
            using var connection = storage.GetConnection();
            var jobs = connection.GetRecurringJobs();
            jobs.ShouldNotContain(job => job.Id == "warehouse-cutoff");
            jobs.ShouldContain(job => job.Id == otherId && job.Cron == "0 0 * * *");
        }
        finally
        {
            manager.RemoveIfExists(otherId);
            manager.RemoveIfExists("warehouse-cutoff");
            JobStorage.Current = sharedStorage;
        }
    }

    [Fact]
    public async Task PersistedJobSignature_Should_RunRepeatedly_WithoutExecutionServices()
    {
        // No tenant store, scope factory, mediator or database is available to this compatibility job.
        var payload = Job.FromExpression<CutoffJob>(job => job.RunAsync(CancellationToken.None));
        var instance = new CutoffJob(NullLogger<CutoffJob>.Instance);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var task = (Task)payload.Method.Invoke(instance, [CancellationToken.None])!;
            await task;
            task.IsCompletedSuccessfully.ShouldBeTrue();
        }
    }
}
