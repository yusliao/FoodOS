using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.AllocateReservation;

public sealed class AllocateReservationCommandHandler(InventoryDbContext dbContext, TimeProvider clock)
    : ICommandHandler<AllocateReservationCommand, AllocateReservationResult>
{
    public async ValueTask<AllocateReservationResult> Handle(
        AllocateReservationCommand command,
        CancellationToken cancellationToken)
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
            var result = await ReplayAsync(command, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        var reservation = await dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == command.ReservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Reservation {command.ReservationId} not found.");

        if (reservation.Released)
        {
            throw new CustomException(
                "Reservation is already released.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        await InventoryAdvisoryLock
            .AcquireSkuLockAsync(
                dbContext,
                reservation.WarehouseId,
                reservation.ProductId,
                reservation.ZoneId,
                cancellationToken)
            .ConfigureAwait(false);

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var balances = await dbContext.LotBalances
            .Where(b =>
                b.WarehouseId == reservation.WarehouseId
                && b.ZoneId == reservation.ZoneId
                && b.ProductId == reservation.ProductId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lotIds = balances.Select(b => b.LotId).Distinct().ToArray();
        var lots = lotIds.Length == 0
            ? new Dictionary<Guid, Lot>()
            : await dbContext.Lots
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken)
                .ConfigureAwait(false);

        var candidates = balances
            .Where(b => lots.ContainsKey(b.LotId))
            .Select(b => new FefoAllocator.Candidate(lots[b.LotId], b));

        IReadOnlyList<FefoAllocator.Slice> slices = FefoAllocator.Take(
            candidates,
            reservation.Quantity,
            today,
            command.MinRemainingDaysOnShip);

        foreach (var slice in slices)
        {
            try
            {
                slice.Balance.AllocateFromAvailable(slice.Quantity);
            }
            catch (InvalidOperationException ex)
            {
                throw new CustomException(ex.Message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
            }

            dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
                InventoryTransactionType.Allocate,
                reservation.ProductId,
                reservation.WarehouseId,
                reservation.ZoneId,
                slice.Quantity,
                $"{command.IdempotencyKey}:lot:{slice.Lot.Id:N}",
                slice.Lot.Id,
                fromBucket: InventoryBucket.Held,
                toBucket: InventoryBucket.Allocated,
                refType: "Reservation",
                refId: reservation.Id));
        }

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
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        decimal allocated = slices.Sum(s => s.Quantity);
        return new AllocateReservationResult(
            reservation.Id,
            slices.Select(s => new LotAllocationDto(s.Lot.Id, s.Lot.LotNo, s.Lot.ExpiryDate, s.Quantity)).ToList(),
            reservation.Quantity - allocated);
    }

    private async Task<AllocateReservationResult> ReplayAsync(
        AllocateReservationCommand command,
        CancellationToken cancellationToken)
    {
        string prefix = $"{command.IdempotencyKey}:lot:";
        var allocTx = await dbContext.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.Type == InventoryTransactionType.Allocate && t.IdempotencyKey.StartsWith(prefix))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lotIds = allocTx.Where(t => t.LotId.HasValue).Select(t => t.LotId!.Value).Distinct().ToArray();
        var lots = lotIds.Length == 0
            ? new Dictionary<Guid, Lot>()
            : await dbContext.Lots.AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken)
                .ConfigureAwait(false);

        var allocations = allocTx
            .Where(t => t.LotId is { } id && lots.ContainsKey(id))
            .Select(t =>
            {
                var lot = lots[t.LotId!.Value];
                return new LotAllocationDto(lot.Id, lot.LotNo, lot.ExpiryDate, t.Quantity);
            })
            .ToList();

        var reservation = await dbContext.Reservations.AsNoTracking()
            .FirstAsync(r => r.Id == command.ReservationId, cancellationToken)
            .ConfigureAwait(false);

        decimal allocated = allocations.Sum(a => a.Quantity);
        return new AllocateReservationResult(
            reservation.Id,
            allocations,
            Math.Max(0, reservation.Quantity - allocated));
    }
}
