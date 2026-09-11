using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReceiveInventory;

public sealed class ReceiveInventoryCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<ReceiveInventoryCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReceiveInventoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool duplicate = await dbContext.InventoryTransactions
            .AnyAsync(t => t.IdempotencyKey == command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            var existing = await dbContext.InventoryTransactions
                .AsNoTracking()
                .FirstAsync(t => t.IdempotencyKey == command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            return existing.LotId ?? existing.Id;
        }

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        var zone = warehouse.ZoneOf(command.Zone);

        string lotNo = command.LotNo.Trim().ToUpperInvariant();
        var lot = await dbContext.Lots
            .FirstOrDefaultAsync(
                l => l.LotNo == lotNo && l.ProductId == command.ProductId,
                cancellationToken)
            .ConfigureAwait(false);

        if (lot is null)
        {
            lot = Lot.Create(command.LotNo, command.ProductId, command.ExpiryDate, command.ManufacturedOn, origin: command.Origin);
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

        balance.Receive(command.Quantity);
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
            refType: "Receive",
            refId: lot.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return lot.Id;
    }
}
