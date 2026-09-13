using System.Diagnostics;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.Events;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Inventory.Jobs;

/// <summary>
/// Daily scan of Active lots within the near-expiry lead window. Alerts only; does not isolate.
/// Skips the Testing host.
/// </summary>
public sealed class NearExpiryJob(
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<NearExpiryJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var tenants = await tenantStore.GetAllAsync().ConfigureAwait(false);
        DateOnly asOf = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        int published = 0;

        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                continue;
            }

            try
            {
                published += await ProcessTenantAsync(tenant, asOf, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Inventory] near-expiry scan failed for tenant {TenantId}", tenant.Id);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Inventory] near-expiry scan published {Count} warehouse(s)", published);
        }
    }

    internal async Task<int> ProcessTenantAsync(
        AppTenantInfo tenant,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var lots = await db.Lots.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var balances = await db.LotBalances.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var hits = NearExpiryScanner.Scan(lots, balances, asOf);
        if (hits.Count == 0)
        {
            return 0;
        }

        int published = 0;
        foreach (var group in hits.GroupBy(h => h.WarehouseId))
        {
            await eventBus.PublishAsync(
                    new NearExpiryLotsDetectedIntegrationEvent(
                        Id: Guid.CreateVersion7(),
                        OccurredOnUtc: DateTime.UtcNow,
                        TenantId: tenant.Id,
                        CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                        Source: "Inventory",
                        WarehouseId: group.Key,
                        AsOf: asOf,
                        Lots: group.Select(h => new NearExpiryLotHitDto(
                            h.LotId,
                            h.LotNo,
                            h.ProductId,
                            h.WarehouseId,
                            h.ExpiryDate,
                            h.AtRiskQty)).ToList()),
                    cancellationToken)
                .ConfigureAwait(false);
            published++;
        }

        return published;
    }
}
