using System.Collections.Concurrent;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Notifications.Contracts.Authorization;
using FSH.Modules.Notifications.Contracts.v1.DTOs;
using FSH.Modules.Notifications.IntegrationEventHandlers;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.Events;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.SignalR.Client;

namespace Integration.Tests.Tests.Notifications;

[Collection(FshCollectionDefinition.Name)]
public sealed class OperationalNotificationIsolationTests(FshWebApplicationFactory factory)
{
    [Fact]
    public async Task ShipmentSummaries_Should_Reach_FullScopeReaders_Not_ShopReaders_Or_AssignedDrivers()
    {
        using var dispatcher = await CreateReaderAsync(LogisticsPermissions.Shipments.View);
        using var shopReader = await CreateReaderAsync(OrderingPermissions.Shop.View);
        using var driver = await CreateReaderAsync(
            LogisticsPermissions.Shipments.ViewAssigned, LogisticsPermissions.ProofOfDelivery.Confirm);
        using var ordinary = await CreateReaderAsync();
        await using var dispatcherHub = await ConnectAsync(dispatcher);
        await using var driverHub = await ConnectAsync(driver);
        await using var shopHub = await ConnectAsync(shopReader);
        var pushed = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unexpected = new ConcurrentQueue<NotificationDto>();
        using var dispatcherSubscription = dispatcherHub.On<NotificationDto>("NotificationCreated", item => pushed.TrySetResult(item));
        using var driverSubscription = driverHub.On<NotificationDto>("NotificationCreated", unexpected.Enqueue);
        using var shopSubscription = shopHub.On<NotificationDto>("NotificationCreated", unexpected.Enqueue);
        using var scope = factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var shipmentId = Guid.NewGuid();
        var stopId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var departed = new ShipmentDepartedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "root",
            "test", "Logistics", shipmentId, "isolated-shipment", warehouseId, [], []);
        var delivered = new ShipmentStopDeliveredIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "root",
            "test", "Logistics", shipmentId, stopId, Guid.NewGuid(), []);
        var due = new PodDueIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "root",
            "test", "Logistics", warehouseId, new DateOnly(2026, 9, 16), 42);

        // Exercise the actual ingress handlers twice: no duplicate inbox items on replay.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await ActivatorUtilities.CreateInstance<ShipmentDepartedNotificationHandler>(scope.ServiceProvider)
                .HandleAsync(departed);
            await ActivatorUtilities.CreateInstance<ShipmentStopDeliveredNotificationHandler>(scope.ServiceProvider)
                .HandleAsync(delivered);
            await ActivatorUtilities.CreateInstance<PodDueNotificationHandler>(scope.ServiceProvider)
                .HandleAsync(due);
        }

        var received = await ReadAsync(dispatcher);
        received.Count.ShouldBe(3);
        var realtime = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.ShouldContain(n => n.Id == realtime.Id);
        await Task.Delay(250);
        unexpected.ShouldBeEmpty("Personal task and Shop permissions must not receive operational SignalR payloads");
        received.ShouldAllBe(n => n.Link != null && n.Link.StartsWith("/ops/shipments", StringComparison.Ordinal));
        (await ReadAsync(shopReader)).ShouldBeEmpty();
        (await ReadAsync(driver)).ShouldBeEmpty();
        (await ReadAsync(ordinary)).ShouldBeEmpty();

        using var foreignRead = await driver.PostAsync($"/api/v1/notifications/{received[0].Id}/read", null);
        foreignRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadAsync(dispatcher)).ShouldAllBe(n => n.ReadAtUtc == null);
    }

    [Fact]
    public async Task CutoffSummary_Should_Require_Both_WaveView_And_SupervisorScope()
    {
        using var supervisor = await CreateReaderAsync(WarehousePermissions.Waves.View, WarehousePermissions.Waves.Assign);
        using var picker = await CreateReaderAsync(WarehousePermissions.Waves.View, WarehousePermissions.Picks.View);
        using var assignOnly = await CreateReaderAsync(WarehousePermissions.Waves.Assign);
        using var scope = factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var cutoff = new DailyCutoffReachedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "root",
            "test", "Warehouse", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 16), 15, 3);
        await ActivatorUtilities.CreateInstance<DailyCutoffReachedNotificationHandler>(scope.ServiceProvider)
            .HandleAsync(cutoff);
        (await ReadAsync(supervisor)).ShouldContain(n => n.Type == "ops.cutoff");
        (await ReadAsync(picker)).ShouldBeEmpty();
        (await ReadAsync(assignOnly)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("customer-a", "customer-a")]
    [InlineData("root", "customer-a")]
    [InlineData("customer-a", "root")]
    [InlineData(null, null)]
    public async Task OperationalIngress_Should_Reject_Customer_Mismatched_And_Missing_Tenants(
        string? ambientTenant, string? eventTenant)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(ambientTenant is null ? null : new AppTenantInfo(ambientTenant, ambientTenant));
        var handler = ActivatorUtilities.CreateInstance<ShipmentDepartedNotificationHandler>(scope.ServiceProvider);
        var message = new ShipmentDepartedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, eventTenant,
            "test", "Logistics", Guid.NewGuid(), "must-not-fanout", Guid.NewGuid(), [], []);
        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(message));
    }

    private Task<HttpClient> CreateReaderAsync(params string[] permissions) =>
        OperatorTestUsers.CreateOperatorAsync(factory,
            [NotificationPermissions.Inbox.View, NotificationPermissions.Inbox.MarkRead, .. permissions]);

    private async Task<HubConnection> ConnectAsync(HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/api/v1/realtime/hub?access_token={Uri.EscapeDataString(token)}", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.Headers["tenant"] = TestConstants.RootTenantId;
            }).Build();
        await connection.StartAsync();
        return connection;
    }

    private static async Task<IReadOnlyList<NotificationDto>> ReadAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/notifications/?pageSize=200");
        response.EnsureSuccessStatusCode();
        return await response.DeserializeAsync<IReadOnlyList<NotificationDto>>();
    }
}
