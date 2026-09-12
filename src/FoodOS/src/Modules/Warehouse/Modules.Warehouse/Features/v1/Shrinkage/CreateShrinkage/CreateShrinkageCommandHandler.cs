using System.Net;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Shrinkage;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Features.v1;
using FSH.Modules.Warehouse.Domain;
using ShrinkageRecord = FSH.Modules.Warehouse.Domain.Shrinkage;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Shrinkage.CreateShrinkage;

public sealed class CreateShrinkageCommandHandler(
    WarehouseDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<CreateShrinkageCommand, ShrinkageDto>
{
    public async ValueTask<ShrinkageDto> Handle(CreateShrinkageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid actorId = currentUser.GetUserId();
        if (actorId == Guid.Empty)
        {
            throw new CustomException(
                "Cannot record shrinkage without an authenticated user.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Unauthorized);
        }

        if (!Enum.TryParse<TemperatureZoneKind>(command.Zone, ignoreCase: true, out var zone))
        {
            throw new CustomException(
                $"Unknown zone '{command.Zone}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var row = ShrinkageRecord.Create(
            command.WarehouseId,
            command.Zone,
            command.ProductId,
            command.LotId,
            command.Quantity,
            command.Reason,
            actorId,
            command.PhotoFileIds);
        dbContext.Shrinkages.Add(row);

        var pendingPutaway = await dbContext.PutawayTasks
            .Where(t => t.LotId == command.LotId && t.Status == PutawayTaskStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var task in pendingPutaway)
        {
            task.Cancel();
        }

        await mediator.Send(
                new AdjustShrinkStockCommand(
                    command.WarehouseId,
                    zone,
                    command.ProductId,
                    command.LotId,
                    command.Quantity,
                    $"shrink:{row.Id:N}",
                    row.Id),
                cancellationToken)
            .ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToDto();
    }
}
