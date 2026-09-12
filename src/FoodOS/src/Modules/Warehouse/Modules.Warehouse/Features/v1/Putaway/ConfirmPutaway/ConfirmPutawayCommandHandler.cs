using System.Net;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Lots;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.ConfirmPutaway;

public sealed class ConfirmPutawayCommandHandler(
    WarehouseDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<ConfirmPutawayCommand, PutawayTaskDto>
{
    public async ValueTask<PutawayTaskDto> Handle(ConfirmPutawayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var task = await dbContext.PutawayTasks
            .FirstOrDefaultAsync(t => t.Id == command.PutawayTaskId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Putaway task {command.PutawayTaskId} not found.");

        if (await mediator.Send(new GetLotIsolationQuery(task.LotId), cancellationToken).ConfigureAwait(false))
        {
            throw new CustomException(
                "Isolated lots cannot be put away.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var location = await dbContext.Locations
            .FirstOrDefaultAsync(l => l.Id == command.LocationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Location {command.LocationId} not found.");

        if (location.WarehouseId != task.WarehouseId || location.ZoneId != task.ZoneId)
        {
            throw new CustomException(
                "Putaway location must be in the same warehouse zone.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        if (location.Type is LocationType.Dock or LocationType.Quarantine)
        {
            throw new CustomException(
                "Putaway must target a storage or pick location.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        task.Complete(location.Id);

        var placement = await dbContext.StockPlacements
            .FirstOrDefaultAsync(
                p => p.LotId == task.LotId && p.LocationId == location.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (placement is null)
        {
            placement = StockPlacement.Create(task.LotId, location.Id, task.Quantity);
            dbContext.StockPlacements.Add(placement);
        }
        else
        {
            placement.Add(task.Quantity);
        }

        string actor = currentUser.GetUserId().ToString("N");
        dbContext.TraceEvents.Add(TraceEvent.Capture(
            task.ProductId,
            "storing",
            "active",
            task.Quantity,
            "EA",
            actor,
            "PutawayTask",
            task.Id,
            task.LotId,
            destLocation: location.Code));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task.ToDto();
    }
}
