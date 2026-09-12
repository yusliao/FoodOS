using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Plans;

public sealed record CreateDailyPlanCommand(Guid WarehouseId, DateOnly? BusinessDate = null) : ICommand<DailyPlanDto>;
