namespace FSH.Modules.Logistics.Contracts.Dtos;

public sealed record CustomerDeliveryDto(
    Guid ShipmentId,
    string ShipmentNumber,
    Guid StoreId,
    DateOnly BusinessDate,
    string ShipmentStatus,
    string StopStatus,
    int Sequence,
    string? DeliveryWindow,
    DateTimeOffset? SignedAt,
    IReadOnlyList<Guid> OrderIds);
