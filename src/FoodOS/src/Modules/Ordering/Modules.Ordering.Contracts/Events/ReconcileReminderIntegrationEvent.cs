using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Ordering.Contracts.Events;

public sealed record ReconcileReminderIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid WarehouseId,
    DateOnly LocalDate,
    int OpenOrderCount) : IIntegrationEvent;
