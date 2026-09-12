using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;
using FSH.Modules.Warehouse.Features.v1.Cutoff;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Warehouse.Jobs;

/// <summary>
/// Every minute: for each tenant warehouse whose local cutoff has elapsed and has no DailyPlan
/// for that local date, run the same <see cref="ConfirmCutoffCommand"/> as the HTTP path.
/// Skips the Testing host so integration suites are not auto-cut off.
/// </summary>
public sealed class CutoffJob(
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<CutoffJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var tenants = await tenantStore.GetAllAsync().ConfigureAwait(false);
        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        int triggered = 0;

        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                continue;
            }

            try
            {
                triggered += await ProcessTenantAsync(tenant, utcNow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Warehouse] cutoff scan failed for tenant {TenantId}", tenant.Id);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Warehouse] cutoff scan triggered {Count} warehouse(s)", triggered);
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
        var warehouses = await mediator.Send(new ListWarehousesQuery(), cancellationToken).ConfigureAwait(false);
        int count = 0;

        foreach (var warehouse in warehouses)
        {
            if (!WarehouseCutoffClock.IsPastCutoff(warehouse.Clock, utcNow))
            {
                continue;
            }

            DateOnly localToday = WarehouseCutoffClock.LocalDate(warehouse.Clock, utcNow);
            var plan = await mediator
                .Send(new GetDailyPlanQuery(warehouse.Id, localToday), cancellationToken)
                .ConfigureAwait(false);
            if (plan is not null)
            {
                continue;
            }

            await mediator
                .Send(new ConfirmCutoffCommand(warehouse.Id, localToday), cancellationToken)
                .ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
