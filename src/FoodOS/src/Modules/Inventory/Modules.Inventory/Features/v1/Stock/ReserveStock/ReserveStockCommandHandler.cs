using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReserveStock;

public sealed class ReserveStockCommandHandler(InventoryDbContext dbContext, TimeProvider clock)
    : ICommandHandler<ReserveStockCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReserveStockCommand command, CancellationToken cancellationToken)
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

        var warehouse = await dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {command.WarehouseId} not found.");

        var zone = warehouse.ZoneOf(command.Zone);

        await InventoryAdvisoryLock
            .AcquireSkuLockAsync(dbContext, warehouse.Id, command.ProductId, zone.Id, cancellationToken)
            .ConfigureAwait(false);

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        decimal available = await StockAvailability.ComputeAsync(
                dbContext,
                warehouse.Id,
                command.ProductId,
                zone.Id,
                today,
                cancellationToken)
            .ConfigureAwait(false);

        if (available < command.Quantity)
        {
            throw new CustomException(
                "Insufficient available quantity to reserve.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var reservation = Reservation.Create(
            warehouse.Id,
            zone.Id,
            command.ProductId,
            command.OrderId,
            command.Quantity,
            command.OrderLineId);
        dbContext.Reservations.Add(reservation);
        dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Reserve,
            command.ProductId,
            warehouse.Id,
            zone.Id,
            command.Quantity,
            command.IdempotencyKey,
            lotId: null,
            fromBucket: InventoryBucket.OnHand,
            toBucket: InventoryBucket.Held,
            refType: "Reservation",
            refId: reservation.Id));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return reservation.Id;
    }
}
