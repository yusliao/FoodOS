namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record StoreDto(
    Guid Id,
    string? CustomerTenantId,
    Guid CustomerOrgId,
    string Code,
    string Name,
    string Address,
    Guid DefaultWarehouseId,
    Guid? DefaultRouteId,
    string? DeliveryWindow,
    DateTime CreatedAtUtc);
