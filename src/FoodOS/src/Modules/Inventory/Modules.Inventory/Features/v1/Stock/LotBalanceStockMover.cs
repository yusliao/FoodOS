using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock;

internal static class LotBalanceStockMover
{
    public static async ValueTask<Guid> MoveAsync(
        InventoryDbContext dbContext,
        Guid warehouseId,
        TemperatureZoneKind zoneKind,
        Guid productId,
        Guid lotId,
        decimal quantity,
        string idempotencyKey,
        Guid? refId,
        InventoryTransactionType type,
        InventoryBucket fromBucket,
        InventoryBucket? toBucket,
        string refType,
        Action<LotBalance, decimal> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(mutate);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(refType);

        await using var tx = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var duplicate = await dbContext.InventoryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return duplicate.LotId ?? duplicate.RefId ?? duplicate.Id;
        }

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {warehouseId} not found.");

        var zone = warehouse.ZoneOf(zoneKind);

        var lot = await dbContext.Lots
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Lot {lotId} not found.");

        if (lot.ProductId != productId)
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
            ?? throw new NotFoundException($"Lot balance for {lotId} was not found in this warehouse zone.");

        await InventoryAdvisoryLock
            .AcquireSkuLockAsync(dbContext, warehouse.Id, lot.ProductId, zone.Id, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            mutate(balance, quantity);
        }
        catch (InvalidOperationException ex)
        {
            throw new CustomException(ex.Message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }

        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            type,
            lot.ProductId,
            warehouse.Id,
            zone.Id,
            quantity,
            idempotencyKey,
            lot.Id,
            fromBucket,
            toBucket,
            refType,
            refId ?? lot.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return lot.Id;
    }
}
