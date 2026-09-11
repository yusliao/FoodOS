namespace FSH.Modules.Warehouse.Contracts.Dtos;

public sealed record PickedLotDto(
    Guid OrderId,
    Guid OrderLineId,
    Guid ProductId,
    string Zone,
    Guid LotId,
    string LotNo,
    decimal Quantity);
