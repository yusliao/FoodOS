using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;
using Mediator;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff.ConfirmCutoff;

public sealed class ConfirmCutoffCommandHandler(IMediator mediator)
    : ICommandHandler<ConfirmCutoffCommand, CutoffResultDto>
{
    public async ValueTask<CutoffResultDto> Handle(ConfirmCutoffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await mediator
            .Send(new CreateDailyPlanCommand(command.WarehouseId), cancellationToken)
            .ConfigureAwait(false);

        int locked = await mediator
            .Send(new LockOrdersForCutoffCommand(command.WarehouseId, plan.BusinessDate), cancellationToken)
            .ConfigureAwait(false);

        return new CutoffResultDto(plan.Id, plan.WarehouseId, plan.BusinessDate, plan.CutoffAt, locked);
    }
}
