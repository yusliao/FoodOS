using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class ReconcileReminderNotificationHandler(
    OperationalInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<ReconcileReminderIntegrationEvent>
{
    public Task HandleAsync(ReconcileReminderIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        DailyCutoffReachedNotificationHandler.EnsureTenant(
            @event.TenantId, tenantAccessor, nameof(ReconcileReminderIntegrationEvent));
        return inbox.FanoutAsync(
            OrderingPermissions.Orders.Reconcile,
            "ops.reconcile",
            "Reconcile reminder",
            $"{@event.OpenOrderCount} received order(s) still open at {@event.LocalDate:yyyy-MM-dd}.",
            $"/shop/orders?reconcile={@event.WarehouseId:N}-{@event.LocalDate:yyyyMMdd}",
            @event.Source,
            new { warehouseId = @event.WarehouseId, localDate = @event.LocalDate, openOrderCount = @event.OpenOrderCount },
            ct);
    }
}
