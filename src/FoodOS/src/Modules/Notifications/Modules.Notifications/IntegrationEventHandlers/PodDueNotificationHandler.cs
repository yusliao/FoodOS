using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class PodDueNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<PodDueIntegrationEvent>
{
    public Task HandleAsync(PodDueIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        DailyCutoffReachedNotificationHandler.EnsureTenant(
            @event.TenantId, tenantAccessor, nameof(PodDueIntegrationEvent));
        return inbox.FanoutAsync(
            LogisticsPermissions.ProofOfDelivery.Confirm,
            "ops.pod-due",
            "Delivery window started",
            $"{@event.OpenStopCount} stop(s) still need proof of delivery for {@event.BusinessDate:yyyy-MM-dd}.",
            $"/ops/shipments?pod={@event.WarehouseId:N}-{@event.BusinessDate:yyyyMMdd}",
            @event.Source,
            new { warehouseId = @event.WarehouseId, businessDate = @event.BusinessDate, openStopCount = @event.OpenStopCount },
            ct);
    }
}
