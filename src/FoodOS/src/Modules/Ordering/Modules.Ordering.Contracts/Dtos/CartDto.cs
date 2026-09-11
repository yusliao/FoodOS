namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record CartDto(
    Guid Id,
    Guid StoreId,
    IReadOnlyList<CartLineDto> Lines,
    DateTimeOffset UpdatedAt);
