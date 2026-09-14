using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.AdjustCountStock;

public sealed class AdjustCountStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<AdjustCountStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(AdjustCountStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var duplicate = await dbContext.InventoryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            return duplicate.LotId ?? duplicate.Id;
        }

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        var zone = warehouse.ZoneOf(command.Zone);
        var balance = await dbContext.LotBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.WarehouseId == warehouse.Id && b.ZoneId == zone.Id && b.LotId == command.LotId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Lot balance for {command.LotId} was not found in this warehouse zone.");

        decimal delta = command.CountedAvailable - balance.Available;
        if (delta == 0)
        {
            return balance.LotId;
        }

        bool gain = delta > 0;
        return await LotBalanceStockMover.MoveAsync(
            dbContext,
            command.WarehouseId,
            command.Zone,
            command.ProductId,
            command.LotId,
            Math.Abs(delta),
            command.IdempotencyKey,
            command.LotId,
            InventoryTransactionType.AdjustCount,
            InventoryBucket.OnHand,
            gain ? InventoryBucket.OnHand : null,
            "Count",
            gain
                ? static (row, qty) => row.CountGain(qty)
                : static (row, qty) => row.CountLoss(qty),
            cancellationToken).ConfigureAwait(false);
    }
}
