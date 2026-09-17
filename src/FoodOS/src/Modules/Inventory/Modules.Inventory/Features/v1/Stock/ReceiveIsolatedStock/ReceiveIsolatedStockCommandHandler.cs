using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReceiveIsolatedStock;

public sealed class ReceiveIsolatedStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<ReceiveIsolatedStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReceiveIsolatedStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        var zone = warehouse.ZoneOf(command.Zone);
        var replay = await ReceiptReplay.FindAsync(
            dbContext, command.IdempotencyKey, warehouse.Id, zone.Id, command.ProductId,
            command.LotNo, command.Quantity, command.ExpiryDate, command.ManufacturedOn,
            "ReceiveIsolated", cancellationToken).ConfigureAwait(false);
        if (replay.HasValue) return replay.Value;

        string lotNo = command.LotNo.Trim().ToUpperInvariant();
        var lot = await dbContext.Lots
            .FirstOrDefaultAsync(
                l => l.LotNo == lotNo && l.ProductId == command.ProductId,
                cancellationToken)
            .ConfigureAwait(false);

        if (lot is null)
        {
            lot = Lot.Create(
                command.LotNo,
                command.ProductId,
                command.ExpiryDate,
                command.ManufacturedOn,
                command.SupplierId,
                command.Origin);
            dbContext.Lots.Add(lot);
        }

        var balance = await dbContext.LotBalances
            .FirstOrDefaultAsync(
                b => b.WarehouseId == warehouse.Id && b.ZoneId == zone.Id && b.LotId == lot.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (balance is null)
        {
            balance = LotBalance.Create(warehouse.Id, zone.Id, lot.Id, command.ProductId);
            dbContext.LotBalances.Add(balance);
        }

        balance.ReceiveIsolated(command.Quantity);
        if (balance.IsFullyIsolated)
        {
            lot.Isolate();
        }

        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Receive,
            command.ProductId,
            warehouse.Id,
            zone.Id,
            command.Quantity,
            command.IdempotencyKey,
            lot.Id,
            fromBucket: null,
            toBucket: InventoryBucket.OnHand,
            refType: "ReceiveIsolated",
            refId: lot.Id));
        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Isolate,
            command.ProductId,
            warehouse.Id,
            zone.Id,
            command.Quantity,
            $"{command.IdempotencyKey}:isolate",
            lot.Id,
            fromBucket: InventoryBucket.OnHand,
            toBucket: InventoryBucket.Isolated,
            refType: "ReceiveIsolated",
            refId: lot.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return lot.Id;
    }
}
