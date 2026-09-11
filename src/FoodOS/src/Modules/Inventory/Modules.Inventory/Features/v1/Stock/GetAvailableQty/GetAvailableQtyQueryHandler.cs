using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.GetAvailableQty;

public sealed class GetAvailableQtyQueryHandler(InventoryDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetAvailableQtyQuery, AvailableQtyDto>
{
    public async ValueTask<AvailableQtyDto> Handle(GetAvailableQtyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var balancesQuery = dbContext.LotBalances.AsNoTracking()
            .Where(b => b.WarehouseId == query.WarehouseId && b.ProductId == query.ProductId);

        if (query.Zone is { } zoneKind)
        {
            var zoneIds = await dbContext.TemperatureZones.AsNoTracking()
                .Where(z => z.WarehouseId == query.WarehouseId && z.Kind == zoneKind)
                .Select(z => z.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            balancesQuery = balancesQuery.Where(b => zoneIds.Contains(b.ZoneId));
        }

        var balances = await balancesQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var lotIds = balances.Select(b => b.LotId).Distinct().ToArray();
        var lots = await dbContext.Lots.AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken)
            .ConfigureAwait(false);

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        decimal available = AvailableQtyCalculator.Compute(balances, lots, today);

        return new AvailableQtyDto(
            query.WarehouseId,
            query.ProductId,
            available,
            query.Zone?.ToString() ?? "All");
    }
}
