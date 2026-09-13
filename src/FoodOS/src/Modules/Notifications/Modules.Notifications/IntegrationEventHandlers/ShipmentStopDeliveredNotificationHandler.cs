using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Ordering.Contracts.Authorization;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class ShipmentStopDeliveredNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<ShipmentStopDeliveredIntegrationEvent>
{
    public Task HandleAsync(ShipmentStopDeliveredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        DailyCutoffReachedNotificationHandler.EnsureTenant(
            @event.TenantId, tenantAccessor, nameof(ShipmentStopDeliveredIntegrationEvent));
        return inbox.FanoutAsync(
            OrderingPermissions.Shop.View,
            "ops.delivered",
            "Delivery signed",
            "A stop on your route was signed.",
            $"/shop/orders?delivered={@event.StopId:N}",
            @event.Source,
            new { shipmentId = @event.ShipmentId, stopId = @event.StopId, storeId = @event.StoreId },
            ct);
    }
}
