namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record StoreDto(
    Guid Id,
    Guid CustomerOrgId,
    string Code,
    string Name,
    string Address,
    Guid DefaultWarehouseId,
    Guid? DefaultRouteId,
    string? DeliveryWindow,
    DateTime CreatedAtUtc);
