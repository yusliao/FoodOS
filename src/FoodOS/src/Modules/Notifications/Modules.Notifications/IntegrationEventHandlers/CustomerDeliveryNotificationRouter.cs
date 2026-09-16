using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Logistics.Contracts.Events;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Events;
using Mediator;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class CustomerDeliveryNotificationRouter(
    ICustomerDeliveryNotificationAudience audience, ITenantService tenants, IMediator mediator,
    IEventBus events, IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<ShipmentDepartedIntegrationEvent>,
      IIntegrationEventHandler<ShipmentStopDeliveredIntegrationEvent>
{
    public Task HandleAsync(ShipmentDepartedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return RouteAsync(@event, @event.ShipmentId, null, @event.StoreIds, @event.OrderIds,
            CustomerDeliveryActivity.Departed, ct);
    }

    public Task HandleAsync(ShipmentStopDeliveredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return RouteAsync(@event, @event.ShipmentId, @event.StopId, [@event.StoreId], @event.OrderIds,
            CustomerDeliveryActivity.Delivered, ct);
    }

    private async Task RouteAsync(IIntegrationEvent activity, Guid shipmentId, Guid? stopId,
        IReadOnlyList<Guid> storeIds, IReadOnlyList<Guid> orderIds, CustomerDeliveryActivity kind, CancellationToken ct)
    {
        OperationalNotificationScope.EnsureRootTenant(activity.TenantId, tenantAccessor, nameof(CustomerDeliveryNotificationRouter));
        if (activity.Source != "Logistics" || activity.Id == Guid.Empty)
            throw new InvalidOperationException("Invalid customer delivery source.");

        FSH.Modules.Logistics.Contracts.Dtos.ShipmentDto shipment;
        try
        {
            shipment = await mediator.Send(new GetShipmentByIdQuery(shipmentId), ct).ConfigureAwait(false);
        }
        catch (NotFoundException) { return; }
        if (shipment.Status is not ("Departed" or "Completed")) return;
        var validStores = shipment.Stops.Where(stop => storeIds.Contains(stop.StoreId)
            && (kind == CustomerDeliveryActivity.Departed || (stop.Id == stopId && stop.Status == "Delivered")))
            .Select(stop => stop.StoreId).ToHashSet();
        var validOrders = shipment.Lines.Where(line => validStores.Contains(line.StoreId) && orderIds.Contains(line.OrderId))
            .Select(line => line.OrderId).Distinct().ToArray();
        var targets = await audience.GetOrdersAsync(validOrders, ct).ConfigureAwait(false);
        foreach (var group in targets.GroupBy(target => target.CustomerTenantId))
        {
            var tenantId = await tenants.FindSharedCustomerTenantIdAsync(group.Key, ct).ConfigureAwait(false);
            if (tenantId is null) continue;
            foreach (var target in group)
            {
                if (!shipment.Lines.Any(line => line.OrderId == target.OrderId && line.StoreId == target.StoreId
                    && validStores.Contains(line.StoreId))) continue;
                // Shipment/order/activity is stable even when an upstream retry publishes a new event ID.
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"customer-delivery:{shipmentId:N}:{target.OrderId:N}:{kind}:{tenantId}"));
                await events.PublishAsync(new CustomerOrderDeliveryIntegrationEvent(new Guid(hash.AsSpan(0, 16)),
                    activity.OccurredOnUtc, tenantId, activity.CorrelationId, "Logistics",
                    target.OrderId, target.StoreId, kind), ct).ConfigureAwait(false);
            }
        }
    }
}
