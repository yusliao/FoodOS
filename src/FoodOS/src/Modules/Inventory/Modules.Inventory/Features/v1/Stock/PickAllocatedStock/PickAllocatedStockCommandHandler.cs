using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.PickAllocatedStock;

public sealed class PickAllocatedStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<PickAllocatedStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(PickAllocatedStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var tx = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var duplicate = await dbContext.InventoryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return duplicate.LotId ?? duplicate.RefId ?? duplicate.Id;
        }

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        var zone = warehouse.ZoneOf(command.Zone);

        var lot = await dbContext.Lots
            .FirstOrDefaultAsync(l => l.Id == command.LotId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Lot {command.LotId} not found.");

        if (lot.ProductId != command.ProductId)
        {
            throw new CustomException(
                "Lot does not belong to the requested product.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var balance = await dbContext.LotBalances
            .FirstOrDefaultAsync(
                b => b.WarehouseId == warehouse.Id && b.ZoneId == zone.Id && b.LotId == lot.Id,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Lot balance for {command.LotId} was not found in this warehouse zone.");

        await InventoryAdvisoryLock
            .AcquireSkuLockAsync(dbContext, warehouse.Id, lot.ProductId, zone.Id, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            balance.Pick(command.Quantity);
        }
        catch (InvalidOperationException ex)
        {
            throw new CustomException(ex.Message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }

        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Pick,
            lot.ProductId,
            warehouse.Id,
            zone.Id,
            command.Quantity,
            command.IdempotencyKey,
            lot.Id,
            fromBucket: InventoryBucket.Allocated,
            toBucket: InventoryBucket.Picked,
            refType: "Pick",
            refId: command.RefId ?? lot.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return lot.Id;
    }
}
