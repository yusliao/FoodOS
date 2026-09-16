using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Picks.ConfirmPickTask;

public sealed class ConfirmPickTaskCommandHandler(
    WarehouseDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<ConfirmPickTaskCommand, PickTaskDto>
{
    public async ValueTask<PickTaskDto> Handle(ConfirmPickTaskCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        WaveAccess.RequireOperator(currentUser);
        Guid pickerId = currentUser.GetUserId();

        var wave = await dbContext.Waves
            .FirstOrDefaultAsync(w => w.AssignedPickerUserId == pickerId
                && w.Tasks.Any(t => t.Id == command.PickTaskId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Pick task {command.PickTaskId} not found.");

        var task = wave.Tasks.First(t => t.Id == command.PickTaskId);
        if (task.Status == PickTaskStatus.Picked)
        {
            return task.ToDto();
        }

        task.Confirm(command.ScannedLotId, pickerId);

        if (task.LotId is { } lotId)
        {
            await mediator.Send(
                    new PickAllocatedStockCommand(
                        wave.WarehouseId,
                        ZoneKinds.Parse(task.Zone),
                        task.ProductId,
                        lotId,
                        task.Quantity,
                        $"pick:{task.Id:N}",
                        task.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            dbContext.TraceEvents.Add(TraceEvent.Capture(
                task.ProductId,
                "picking",
                "active",
                task.Quantity,
                "EA",
                pickerId.ToString("N"),
                "PickTask",
                task.Id,
                lotId,
                sourceLocation: task.LocationId.ToString("N")));
        }

        bool orderDone = wave.Tasks.Where(t => t.OrderId == task.OrderId).All(t => t.IsComplete);
        if (orderDone)
        {
            await mediator.Send(new ConfirmOrderPackedCommand(task.OrderId), cancellationToken).ConfigureAwait(false);
        }

        wave.CompleteIfDone();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task.ToDto();
    }
}
