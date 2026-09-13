using System.Diagnostics;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.Events;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Ordering.Jobs;

/// <summary>
/// Every minute: after each warehouse's local reconcile time, remind finance about Received orders
/// that are not yet Reconciled. Skips the Testing host.
/// </summary>
public sealed class ReconcileReminderJob(
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<ReconcileReminderJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var tenants = await tenantStore.GetAllAsync().ConfigureAwait(false);
        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        int published = 0;

        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                continue;
            }

            try
            {
                published += await ProcessTenantAsync(tenant, utcNow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Ordering] reconcile reminder failed for tenant {TenantId}", tenant.Id);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Ordering] reconcile reminder published {Count} warehouse(s)", published);
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
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var warehouses = await mediator.Send(new ListWarehousesQuery(), cancellationToken).ConfigureAwait(false);
        int count = 0;

        foreach (var warehouse in warehouses)
        {
            if (!OperatingClockEvaluator.IsPastLocalTime(warehouse.Clock, warehouse.Clock.ReconcileLocal, utcNow))
            {
                continue;
            }

            DateOnly localToday = OperatingClockEvaluator.LocalDate(warehouse.Clock, utcNow);
            bool already = await db.ReconcileReminderLogs
                .AsNoTracking()
                .AnyAsync(x => x.WarehouseId == warehouse.Id && x.LocalDate == localToday, cancellationToken)
                .ConfigureAwait(false);
            if (already)
            {
                continue;
            }

            int open = await db.SalesOrders
                .CountAsync(
                    o => o.WarehouseId == warehouse.Id && o.Status == SalesOrderStatus.Received,
                    cancellationToken)
                .ConfigureAwait(false);
            if (open == 0)
            {
                continue;
            }

            db.ReconcileReminderLogs.Add(ReconcileReminderLog.Record(warehouse.Id, localToday, open));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                    new ReconcileReminderIntegrationEvent(
                        Id: Guid.CreateVersion7(),
                        OccurredOnUtc: DateTime.UtcNow,
                        TenantId: tenant.Id,
                        CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                        Source: "Ordering",
                        WarehouseId: warehouse.Id,
                        LocalDate: localToday,
                        OpenOrderCount: open),
                    cancellationToken)
                .ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
