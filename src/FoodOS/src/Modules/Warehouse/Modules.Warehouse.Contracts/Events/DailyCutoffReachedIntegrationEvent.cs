using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Warehouse.Contracts.Events;

public sealed record DailyCutoffReachedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid WarehouseId,
    Guid DailyPlanId,
    DateOnly BusinessDate,
    int OrdersLocked,
    int WavesGenerated = 0) : IIntegrationEvent;
