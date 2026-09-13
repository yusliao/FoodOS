using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Logistics.Contracts.Events;

public sealed record ShipmentStopDeliveredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid ShipmentId,
    Guid StopId,
    Guid StoreId,
    IReadOnlyList<Guid> OrderIds) : IIntegrationEvent;
