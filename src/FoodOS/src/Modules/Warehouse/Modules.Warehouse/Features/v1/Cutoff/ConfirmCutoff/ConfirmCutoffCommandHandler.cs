using System.Diagnostics;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Events;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;
using Mediator;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff.ConfirmCutoff;

public sealed class ConfirmCutoffCommandHandler(
    IMediator mediator,
    IEventBus eventBus,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<ConfirmCutoffCommand, CutoffResultDto>
{
    public async ValueTask<CutoffResultDto> Handle(ConfirmCutoffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await mediator
            .Send(new CreateDailyPlanCommand(command.WarehouseId, command.BusinessDate), cancellationToken)
            .ConfigureAwait(false);

        int locked = await mediator
            .Send(new LockOrdersForCutoffCommand(command.WarehouseId, plan.BusinessDate), cancellationToken)
            .ConfigureAwait(false);

        var result = new CutoffResultDto(plan.Id, plan.WarehouseId, plan.BusinessDate, plan.CutoffAt, locked);

        await eventBus.PublishAsync(
                new DailyCutoffReachedIntegrationEvent(
                    Id: Guid.CreateVersion7(),
                    OccurredOnUtc: DateTime.UtcNow,
                    TenantId: tenantAccessor.MultiTenantContext.TenantInfo?.Id,
                    CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    Source: "Warehouse",
                    WarehouseId: result.WarehouseId,
                    DailyPlanId: result.DailyPlanId,
                    BusinessDate: result.BusinessDate,
                    OrdersLocked: result.OrdersLocked),
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
