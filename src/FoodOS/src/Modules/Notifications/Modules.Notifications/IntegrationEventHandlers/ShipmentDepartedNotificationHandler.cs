using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Ordering.Contracts.Authorization;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class ShipmentDepartedNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<ShipmentDepartedIntegrationEvent>
{
    public Task HandleAsync(ShipmentDepartedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        DailyCutoffReachedNotificationHandler.EnsureTenant(
            @event.TenantId, tenantAccessor, nameof(ShipmentDepartedIntegrationEvent));
        return inbox.FanoutAsync(
            OrderingPermissions.Shop.View,
            "ops.departed",
            "Shipment departed",
            $"Shipment {@event.ShipmentNumber} is in transit.",
            $"/shop/orders?departed={@event.ShipmentId:N}",
            @event.Source,
            new { shipmentId = @event.ShipmentId, warehouseId = @event.WarehouseId },
            ct);
    }
}
