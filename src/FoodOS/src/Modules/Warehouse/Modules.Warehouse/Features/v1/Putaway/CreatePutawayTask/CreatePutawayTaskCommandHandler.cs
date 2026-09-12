using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Lots;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.CreatePutawayTask;

public sealed class CreatePutawayTaskCommandHandler(WarehouseDbContext dbContext, IMediator mediator)
    : ICommandHandler<CreatePutawayTaskCommand, PutawayTaskDto>
{
    public async ValueTask<PutawayTaskDto> Handle(
        CreatePutawayTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await mediator.Send(new GetLotIsolationQuery(command.LotId), cancellationToken).ConfigureAwait(false))
        {
            throw new CustomException(
                "Isolated lots cannot be put away.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var existing = await dbContext.PutawayTasks
            .FirstOrDefaultAsync(
                t => t.LotId == command.LotId && t.Status == PutawayTaskStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var warehouse = await mediator
            .Send(new GetWarehouseByIdQuery(command.WarehouseId), cancellationToken)
            .ConfigureAwait(false);
        var zone = warehouse.Zones.FirstOrDefault(z =>
                string.Equals(z.Kind, command.Zone, StringComparison.OrdinalIgnoreCase)
                || string.Equals(z.Code, command.Zone, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomException(
                $"Warehouse has no zone '{command.Zone}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);

        var suggested = await dbContext.Locations
            .Where(l => l.WarehouseId == warehouse.Id && l.ZoneId == zone.Id)
            .OrderBy(l => l.Type)
            .ThenBy(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var task = PutawayTask.Create(
            warehouse.Id,
            zone.Id,
            zone.Kind,
            command.ProductId,
            command.LotId,
            command.Quantity,
            command.Source,
            suggested?.Id,
            command.RefId);
        dbContext.PutawayTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task.ToDto();
    }
}
