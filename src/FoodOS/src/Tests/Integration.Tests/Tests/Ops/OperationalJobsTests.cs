using Hangfire;
using Hangfire.Storage;
using FSH.Modules.Notifications.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ops;

[Collection(FshCollectionDefinition.Name)]
public sealed class OperationalJobsTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OperationalJobsTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public void RecurringJobs_Should_RegisterCutoffReconcileAndNearExpiry()
    {
        _ = _factory.Server;
        var jobs = JobStorage.Current.GetConnection().GetRecurringJobs();
        jobs.ShouldContain(j => j.Id == "warehouse-cutoff" && j.Cron == "* * * * *");
        jobs.ShouldContain(j => j.Id == "ordering-reconcile-reminder" && j.Cron == "* * * * *");
        jobs.ShouldContain(j => j.Id == "inventory-near-expiry" && j.Cron == "15 7 * * *");
    }

    [Fact]
    public async Task ConfirmCutoff_Should_WriteOpsCutoffInboxRow()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new
            {
                code = $"WH{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = $"Dc-{Guid.NewGuid().ToString("N")[..8]}",
                city = "Boston",
                timeZoneId = (string?)null
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var warehouseId = await create.DeserializeAsync<Guid>();

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouseId}/cutoff",
            new { });
        cutoff.StatusCode.ShouldBe(HttpStatusCode.OK, await cutoff.Content.ReadAsStringAsync());

        using var inbox = await client.GetAsync("/api/v1/notifications/");
        inbox.StatusCode.ShouldBe(HttpStatusCode.OK, await inbox.Content.ReadAsStringAsync());
        var rows = await inbox.DeserializeAsync<List<NotificationDto>>();
        rows.ShouldContain(n => n.Type == "ops.cutoff");
    }
}
