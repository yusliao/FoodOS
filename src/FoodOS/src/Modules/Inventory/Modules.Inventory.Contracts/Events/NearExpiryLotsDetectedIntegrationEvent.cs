using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Inventory.Contracts.Events;

public sealed record NearExpiryLotHitDto(
    Guid LotId,
    string LotNo,
    Guid ProductId,
    Guid WarehouseId,
    DateOnly ExpiryDate,
    decimal AtRiskQty);

public sealed record NearExpiryLotsDetectedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid WarehouseId,
    DateOnly AsOf,
    IReadOnlyList<NearExpiryLotHitDto> Lots) : IIntegrationEvent;
