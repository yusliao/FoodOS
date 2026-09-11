using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Plans.GetDailyPlan;

public sealed class GetDailyPlanQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<GetDailyPlanQuery, DailyPlanDto?>
{
    public async ValueTask<DailyPlanDto?> Handle(GetDailyPlanQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var plan = await dbContext.DailyPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.WarehouseId == query.WarehouseId && p.BusinessDate == query.BusinessDate,
                cancellationToken)
            .ConfigureAwait(false);

        return plan?.ToDto();
    }
}
