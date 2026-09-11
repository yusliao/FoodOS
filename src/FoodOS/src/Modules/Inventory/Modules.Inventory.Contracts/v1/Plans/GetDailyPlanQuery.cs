using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Plans;

public sealed record GetDailyPlanQuery(Guid WarehouseId, DateOnly BusinessDate) : IQuery<DailyPlanDto?>;
