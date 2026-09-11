namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record DailyPlanDto(
    Guid Id,
    Guid WarehouseId,
    DateOnly BusinessDate,
    DateTimeOffset CutoffAt,
    string Status);
