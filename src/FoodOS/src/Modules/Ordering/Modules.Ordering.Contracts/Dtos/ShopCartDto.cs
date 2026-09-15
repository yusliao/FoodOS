namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopCartLineDto(Guid ProductId, decimal Quantity);

public sealed record ShopCartDto(
    Guid Id,
    Guid StoreId,
    IReadOnlyList<ShopCartLineDto> Lines,
    DateTimeOffset UpdatedAt);
