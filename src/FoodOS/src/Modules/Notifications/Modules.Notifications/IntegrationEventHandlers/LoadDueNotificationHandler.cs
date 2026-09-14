using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class LoadDueNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<LoadDueIntegrationEvent>
{
    public Task HandleAsync(LoadDueIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        DailyCutoffReachedNotificationHandler.EnsureTenant(
            @event.TenantId, tenantAccessor, nameof(LoadDueIntegrationEvent));
        return inbox.FanoutAsync(
            LogisticsPermissions.Shipments.Load,
            "ops.load-due",
            "Load time reached",
            $"{@event.OpenShipmentCount} shipment(s) still need loading for {@event.BusinessDate:yyyy-MM-dd}.",
            $"/ops/shipments?load={@event.WarehouseId:N}-{@event.BusinessDate:yyyyMMdd}",
            @event.Source,
            new { warehouseId = @event.WarehouseId, businessDate = @event.BusinessDate, openShipmentCount = @event.OpenShipmentCount },
            ct);
    }
}
