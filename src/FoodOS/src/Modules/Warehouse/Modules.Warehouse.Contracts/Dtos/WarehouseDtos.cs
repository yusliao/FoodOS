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
    DateOnly BusinessDate,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PickTaskDto> Tasks);
