namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record LotAllocationDto(
    Guid LotId,
    string LotNo,
    DateOnly ExpiryDate,
    decimal Quantity);
