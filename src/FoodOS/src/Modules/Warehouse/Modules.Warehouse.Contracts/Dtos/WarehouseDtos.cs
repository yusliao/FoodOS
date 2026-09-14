namespace FSH.Modules.Warehouse.Contracts.Dtos;

public sealed record LocationDto(
    Guid Id,
    Guid WarehouseId,
    Guid ZoneId,
    string Code,
    string Type);

public sealed record CutoffResultDto(
    Guid DailyPlanId,
    Guid WarehouseId,
    DateOnly BusinessDate,
    DateTimeOffset CutoffAt,
    int OrdersLocked);

public sealed record PickTaskDto(
    Guid Id,
    Guid WaveId,
    Guid OrderId,
    Guid OrderLineId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    string? LotNo,
    decimal Quantity,
    decimal ShortageQty,
    string Status);

public sealed record WaveDto(
    Guid Id,
    string Number,
    Guid DailyPlanId,
    Guid WarehouseId,
    Guid ZoneId,
    string Zone,
    Guid? RouteId,
    DateOnly BusinessDate,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PickTaskDto> Tasks);

public sealed record PutawayTaskDto(
    Guid Id,
    Guid WarehouseId,
    Guid ZoneId,
    string Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    Guid? SuggestedLocationId,
    Guid? LocationId,
    string Source,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record PackToteDto(
    Guid Id,
    Guid WaveId,
    string Sscc,
    Guid? DockLocationId,
    string Status,
    IReadOnlyList<Guid> OrderIds,
    DateTimeOffset PackedAt);

public sealed record ShrinkageDto(
    Guid Id,
    Guid WarehouseId,
    string Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string Reason,
    IReadOnlyList<Guid> PhotoFileIds,
    DateTimeOffset CreatedAt);

public sealed record PackedToteOrderDto(Guid OrderId, Guid ToteId);
