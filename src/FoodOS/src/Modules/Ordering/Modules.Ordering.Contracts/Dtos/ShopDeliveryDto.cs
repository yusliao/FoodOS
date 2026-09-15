namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopDeliveryDto(
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
