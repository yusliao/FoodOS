using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class DailyCutoffReachedNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<DailyCutoffReachedIntegrationEvent>
{
    public Task HandleAsync(DailyCutoffReachedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OperationalNotificationScope.EnsureRootTenant(@event.TenantId, tenantAccessor, nameof(DailyCutoffReachedIntegrationEvent));
        return inbox.FanoutAsync(
            WarehousePermissions.Waves.View,
            "ops.cutoff",
            "Cutoff reached",
            $"{@event.OrdersLocked} order(s) locked; {@event.WavesGenerated} draft wave(s) for {@event.BusinessDate:yyyy-MM-dd}.",
            $"/ops/waves?cutoff={@event.DailyPlanId:N}",
            @event.Source,
            new { warehouseId = @event.WarehouseId, dailyPlanId = @event.DailyPlanId, businessDate = @event.BusinessDate, wavesGenerated = @event.WavesGenerated },
            ct,
            scopePermission: WarehousePermissions.Waves.Assign);
    }
}
