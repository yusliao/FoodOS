using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock;

internal static class StockAvailability
{
    public static async Task<decimal> ComputeAsync(
        InventoryDbContext dbContext,
        Guid warehouseId,
        Guid productId,
        Guid? zoneId,
        DateOnly today,
        CancellationToken cancellationToken,
        int minRemainingDaysOnShip = 0)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var balancesQuery = dbContext.LotBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && b.ProductId == productId);
        var reservationsQuery = dbContext.Reservations.AsNoTracking()
            .Where(r => r.WarehouseId == warehouseId && r.ProductId == productId && !r.Released);

        if (zoneId is { } z)
        {
            balancesQuery = balancesQuery.Where(b => b.ZoneId == z);
            reservationsQuery = reservationsQuery.Where(r => r.ZoneId == z);
        }

        var balances = await balancesQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var lotIds = balances.Select(b => b.LotId).Distinct().ToArray();
        var lots = lotIds.Length == 0
            ? new Dictionary<Guid, Lot>()
            : await dbContext.Lots.AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken)
                .ConfigureAwait(false);

        decimal skuReserved = await reservationsQuery
            .SumAsync(r => r.Quantity, cancellationToken)
            .ConfigureAwait(false);

        return AvailableQtyCalculator.Compute(balances, lots, today, minRemainingDaysOnShip, skuReserved);
    }
}
