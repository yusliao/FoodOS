namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record CartLineInput(Guid ProductId, decimal Quantity);
