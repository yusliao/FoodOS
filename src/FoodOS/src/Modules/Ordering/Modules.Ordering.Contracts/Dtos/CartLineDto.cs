namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record CartLineDto(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    string Zone);
