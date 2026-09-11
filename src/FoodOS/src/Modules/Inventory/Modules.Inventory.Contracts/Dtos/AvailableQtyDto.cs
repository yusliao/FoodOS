namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record AvailableQtyDto(
    Guid WarehouseId,
    Guid ProductId,
    decimal Available,
    string ZoneKind);
