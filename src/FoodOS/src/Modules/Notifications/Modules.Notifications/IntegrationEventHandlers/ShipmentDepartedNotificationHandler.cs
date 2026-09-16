using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Logistics.Contracts.Authorization;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class ShipmentDepartedNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<ShipmentDepartedIntegrationEvent>
{
    public Task HandleAsync(ShipmentDepartedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OperationalNotificationScope.EnsureRootTenant(
            @event.TenantId, tenantAccessor, nameof(ShipmentDepartedIntegrationEvent));
        return inbox.FanoutAsync(
            LogisticsPermissions.Shipments.View,
            "ops.departed",
            "Shipment departed",
            $"Shipment {@event.ShipmentNumber} is in transit.",
            $"/ops/shipments?departed={@event.ShipmentId:N}",
            @event.Source,
            new { shipmentId = @event.ShipmentId, warehouseId = @event.WarehouseId },
            ct);
    }
}
