namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopStoreDto(
    Guid Id,
    string Code,
    string Name,
    string Address,
    string? DeliveryWindow);
