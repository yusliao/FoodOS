using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class NearExpiryLotsDetectedNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<NearExpiryLotsDetectedIntegrationEvent>
{
    public Task HandleAsync(NearExpiryLotsDetectedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OperationalNotificationScope.EnsureRootTenant(
            @event.TenantId, tenantAccessor, nameof(NearExpiryLotsDetectedIntegrationEvent));
        return inbox.FanoutAsync(
            InventoryPermissions.Stock.View,
            "ops.near-expiry",
            "Lots nearing expiry",
            $"{@event.Lots.Count} lot(s) expire within the lead window (alert only; not isolated).",
            $"/ops/putaway?near-expiry={@event.WarehouseId:N}-{@event.AsOf:yyyyMMdd}",
            @event.Source,
            new { warehouseId = @event.WarehouseId, asOf = @event.AsOf, lotCount = @event.Lots.Count },
            ct);
    }
}
