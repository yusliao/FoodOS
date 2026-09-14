using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Logistics.Contracts.Events;

public sealed record PodDueIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid WarehouseId,
    DateOnly BusinessDate,
    int OpenStopCount) : IIntegrationEvent;
