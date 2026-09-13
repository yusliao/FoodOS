using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Logistics.Contracts.Events;

public sealed record ShipmentDepartedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid ShipmentId,
    string ShipmentNumber,
    Guid WarehouseId,
    IReadOnlyList<Guid> OrderIds,
    IReadOnlyList<Guid> StoreIds) : IIntegrationEvent;
