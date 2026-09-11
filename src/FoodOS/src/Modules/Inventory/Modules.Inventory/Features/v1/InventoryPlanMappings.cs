using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Domain;

namespace FSH.Modules.Inventory.Features.v1;

internal static class InventoryPlanMappings
{
    public static DailyPlanDto ToDto(this DailyPlan plan)
        => new(plan.Id, plan.WarehouseId, plan.BusinessDate, plan.CutoffAt, plan.Status.ToString());
}
