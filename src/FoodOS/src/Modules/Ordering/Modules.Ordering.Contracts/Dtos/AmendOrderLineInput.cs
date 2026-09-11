namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record AmendOrderLineInput(Guid ProductId, decimal Quantity);
