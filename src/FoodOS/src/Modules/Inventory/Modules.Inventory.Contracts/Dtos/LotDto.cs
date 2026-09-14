namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record LotDto(
    Guid Id,
    string LotNo,
    Guid ProductId,
    Guid? SupplierId,
    DateOnly? ManufacturedOn,
    DateOnly ExpiryDate,
    string? Origin,
    string Status,
    DateTime CreatedAtUtc);

public sealed record LotBalanceDto(
    Guid Id,
    Guid WarehouseId,
    Guid ZoneId,
    string ZoneKind,
    Guid LotId,
    Guid ProductId,
    decimal OnHand,
    decimal Reserved,
    decimal Allocated,
    decimal Picked,
    decimal InTransit,
    decimal Isolated,
    decimal Available);

public sealed record LotDetailDto(
    LotDto Lot,
    IReadOnlyList<LotBalanceDto> Balances);

public sealed record InventoryTransactionDto(
    Guid Id,
    string Type,
    Guid ProductId,
    Guid WarehouseId,
    Guid ZoneId,
    Guid? LotId,
    decimal Quantity,
    string? FromBucket,
    string? ToBucket,
    string? RefType,
    Guid? RefId,
    DateTimeOffset OccurredAt);
