using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Ordering.Contracts.Events;

// A customer projection: never carry the mixed shipment, route, driver or POD payload.
public sealed record CustomerOrderDeliveryIntegrationEvent(
    Guid Id, DateTime OccurredOnUtc, string? TenantId, string CorrelationId, string Source,
    Guid OrderId, Guid StoreId, CustomerDeliveryActivity Activity) : IIntegrationEvent;

public enum CustomerDeliveryActivity { Departed, Delivered }
