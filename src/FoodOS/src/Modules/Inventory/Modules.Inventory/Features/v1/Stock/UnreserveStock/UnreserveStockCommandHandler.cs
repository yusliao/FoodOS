using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.UnreserveStock;

public sealed class UnreserveStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<UnreserveStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(UnreserveStockCommand command, CancellationToken cancellationToken)
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
            return duplicate.RefId ?? duplicate.Id;
        }

        var reservation = await dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == command.ReservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Reservation {command.ReservationId} not found.");

        await InventoryAdvisoryLock
            .AcquireSkuLockAsync(
                dbContext,
                reservation.WarehouseId,
                reservation.ProductId,
                reservation.ZoneId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!reservation.Released)
        {
            reservation.Release();
            dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
                InventoryTransactionType.Unreserve,
                reservation.ProductId,
                reservation.WarehouseId,
                reservation.ZoneId,
                reservation.Quantity,
                command.IdempotencyKey,
                lotId: null,
                fromBucket: InventoryBucket.Held,
                toBucket: InventoryBucket.OnHand,
                refType: "Reservation",
                refId: reservation.Id));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return reservation.Id;
    }
}
