using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Waves.StartWave;

public sealed class StartWaveCommandHandler(WarehouseDbContext dbContext, IMediator mediator)
    : ICommandHandler<StartWaveCommand, WaveDto>
{
    public async ValueTask<WaveDto> Handle(StartWaveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wave = await dbContext.Waves
            .FirstOrDefaultAsync(w => w.Id == command.WaveId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Wave {command.WaveId} not found.");

        if (wave.Status is not WaveStatus.Draft)
        {
            return wave.ToDto();
        }

        var pending = wave.Tasks.Where(t => t.Status == PickTaskStatus.Pending && t.LotId is null).ToList();
        var extraTasks = new List<PickTask>();
        foreach (var task in pending)
        {
            if (task.ReservationId is null)
            {
                task.MarkShorted(task.Quantity);
                continue;
            }

            var allocation = await mediator.Send(
                    new AllocateReservationCommand(
                        task.ReservationId.Value,
                        $"wave:{wave.Id:N}:task:{task.Id:N}"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (allocation.Allocations.Count == 0)
            {
                task.MarkShorted(allocation.ShortageQty);
                continue;
            }

            var first = allocation.Allocations[0];
            decimal leftoverShortage = allocation.ShortageQty;
            task.BindAllocation(first.LotId, first.LotNo, first.Quantity, leftoverShortage);

            for (int i = 1; i < allocation.Allocations.Count; i++)
            {
                var slice = allocation.Allocations[i];
                var extra = wave.AddTask(
                    task.OrderId,
                    task.OrderLineId,
                    task.ReservationId,
                    task.ProductId,
                    task.Zone,
                    task.LocationId,
                    slice.Quantity);
                extra.BindAllocation(slice.LotId, slice.LotNo, slice.Quantity, 0);
                extraTasks.Add(extra);
            }
        }

        foreach (var extra in extraTasks)
        {
            dbContext.PickTasks.Add(extra);
        }

        wave.Release();
        wave.MarkPicking();

        var orderIds = wave.Tasks.Select(t => t.OrderId).Distinct();
        foreach (var orderId in orderIds)
        {
            await mediator.Send(new StartOrderPickingCommand(orderId), cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return wave.ToDto();
    }
}
