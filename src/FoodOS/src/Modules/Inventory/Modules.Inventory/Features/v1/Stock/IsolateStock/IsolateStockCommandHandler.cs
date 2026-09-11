using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.IsolateStock;

public sealed class IsolateStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<IsolateStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(IsolateStockCommand command, CancellationToken cancellationToken)
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
            balance.Isolate(command.Quantity);
        }
        catch (InvalidOperationException ex)
        {
            throw new CustomException(ex.Message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }

        if (balance.IsFullyIsolated)
        {
            lot.Isolate();
        }

        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Isolate,
            lot.ProductId,
            warehouse.Id,
            zone.Id,
            command.Quantity,
            command.IdempotencyKey,
            lot.Id,
            fromBucket: InventoryBucket.OnHand,
            toBucket: InventoryBucket.Isolated,
            refType: "Isolate",
            refId: lot.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return lot.Id;
    }
}
