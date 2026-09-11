namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string City,
    string TimeZoneId,
    OperatingClockDto Clock,
    IReadOnlyList<TemperatureZoneDto> Zones,
    DateTime CreatedAtUtc);
