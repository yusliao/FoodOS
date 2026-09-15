using System.Diagnostics;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Logistics.Jobs;

/// <summary>
/// Every minute: after LoadLocal, remind ops about Created/Loading shipments;
/// after DeliverFromLocal, remind about Departed stops still missing POD.
/// Does not Depart or ConfirmPod. Skips the Testing host.
/// </summary>
public sealed class DispatchReminderJob(
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<DispatchReminderJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var operatorTenant = await tenantStore.GetAsync(MultitenancyConstants.Root.Id).ConfigureAwait(false);
        if (operatorTenant is null || !operatorTenant.IsActive)
        {
            logger.LogWarning("[Logistics] dispatch reminder skipped because the operator tenant is unavailable");
            return;
        }

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        int published;
        try
        {
            published = await ProcessTenantAsync(operatorTenant, utcNow, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Logistics] dispatch reminder failed for the operator tenant");
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Logistics] dispatch reminder published {Count} event(s)", published);
        }
    }

    internal async Task<int> ProcessTenantAsync(
        AppTenantInfo tenant,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<LogisticsDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var warehouses = await mediator.Send(new ListWarehousesQuery(), cancellationToken).ConfigureAwait(false);
        int count = 0;

        foreach (var warehouse in warehouses)
        {
            DateOnly localToday = OperatingClockEvaluator.LocalDate(warehouse.Clock, utcNow);
            DateOnly businessDate = DispatchReminderPlanner.BusinessDate(warehouse.Clock, utcNow);

            if (OperatingClockEvaluator.IsPastLocalTime(warehouse.Clock, warehouse.Clock.LoadLocal, utcNow))
            {
                count += await TryPublishLoadAsync(
                    tenant.Id,
                    warehouse.Id,
                    localToday,
                    businessDate,
                    db,
                    eventBus,
                    cancellationToken).ConfigureAwait(false);
            }

            if (OperatingClockEvaluator.IsPastLocalTime(warehouse.Clock, warehouse.Clock.DeliverFromLocal, utcNow))
            {
                count += await TryPublishPodAsync(
                    tenant.Id,
                    warehouse.Id,
                    localToday,
                    businessDate,
                    db,
                    eventBus,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return count;
    }

    private static async Task<int> TryPublishLoadAsync(
        string tenantId,
        Guid warehouseId,
        DateOnly localToday,
        DateOnly businessDate,
        LogisticsDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        bool already = await db.DispatchReminderLogs
            .AsNoTracking()
            .AnyAsync(
                x => x.WarehouseId == warehouseId
                     && x.LocalDate == localToday
                     && x.Kind == DispatchReminderLog.LoadKind,
                cancellationToken)
            .ConfigureAwait(false);
        if (already)
        {
            return 0;
        }

        int open = await db.Shipments
            .CountAsync(
                s => s.WarehouseId == warehouseId
                     && s.BusinessDate == businessDate
                     && (s.Status == ShipmentStatus.Created || s.Status == ShipmentStatus.Loading),
                cancellationToken)
            .ConfigureAwait(false);
        if (open == 0)
        {
            return 0;
        }

        db.DispatchReminderLogs.Add(DispatchReminderLog.Record(warehouseId, localToday, DispatchReminderLog.LoadKind, open));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
                new LoadDueIntegrationEvent(
                    Id: Guid.CreateVersion7(),
                    OccurredOnUtc: DateTime.UtcNow,
                    TenantId: tenantId,
                    CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    Source: "Logistics",
                    WarehouseId: warehouseId,
                    BusinessDate: businessDate,
                    OpenShipmentCount: open),
                cancellationToken)
            .ConfigureAwait(false);
        return 1;
    }

    private static async Task<int> TryPublishPodAsync(
        string tenantId,
        Guid warehouseId,
        DateOnly localToday,
        DateOnly businessDate,
        LogisticsDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        bool already = await db.DispatchReminderLogs
            .AsNoTracking()
            .AnyAsync(
                x => x.WarehouseId == warehouseId
                     && x.LocalDate == localToday
                     && x.Kind == DispatchReminderLog.PodKind,
                cancellationToken)
            .ConfigureAwait(false);
        if (already)
        {
            return 0;
        }

        int open = await db.Shipments
            .Where(s =>
                s.WarehouseId == warehouseId
                && s.BusinessDate == businessDate
                && s.Status == ShipmentStatus.Departed)
            .SelectMany(s => s.Stops)
            .CountAsync(st => st.Status == StopStatus.Pending, cancellationToken)
            .ConfigureAwait(false);
        if (open == 0)
        {
            return 0;
        }

        db.DispatchReminderLogs.Add(DispatchReminderLog.Record(warehouseId, localToday, DispatchReminderLog.PodKind, open));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
                new PodDueIntegrationEvent(
                    Id: Guid.CreateVersion7(),
                    OccurredOnUtc: DateTime.UtcNow,
                    TenantId: tenantId,
                    CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    Source: "Logistics",
                    WarehouseId: warehouseId,
                    BusinessDate: businessDate,
                    OpenStopCount: open),
                cancellationToken)
            .ConfigureAwait(false);
        return 1;
    }
}
