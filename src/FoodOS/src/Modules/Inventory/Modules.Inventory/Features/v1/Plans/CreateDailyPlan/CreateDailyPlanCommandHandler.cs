using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Plans.CreateDailyPlan;

public sealed class CreateDailyPlanCommandHandler(InventoryDbContext dbContext, TimeProvider clock)
    : ICommandHandler<CreateDailyPlanCommand, DailyPlanDto>
{
    public async ValueTask<DailyPlanDto> Handle(CreateDailyPlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        DateTimeOffset utcNow = clock.GetUtcNow();
        DateOnly businessDate = command.BusinessDate ?? warehouse.Clock.Resolve(utcNow).BusinessDate;

        var existing = await dbContext.DailyPlans
            .FirstOrDefaultAsync(
                p => p.WarehouseId == warehouse.Id && p.BusinessDate == businessDate,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var plan = DailyPlan.Open(warehouse.Id, businessDate, utcNow);
        dbContext.DailyPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plan.ToDto();
    }
}
