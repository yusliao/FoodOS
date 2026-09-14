using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Lots;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Lots.GetLotById;

public sealed class GetLotByIdQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<GetLotByIdQuery, LotDetailDto>
{
    public async ValueTask<LotDetailDto> Handle(GetLotByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var lot = await dbContext.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.LotId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Lot {query.LotId} not found.");

        var balances = await dbContext.LotBalances
            .AsNoTracking()
            .Where(b => b.LotId == lot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var zoneIds = balances.Select(b => b.ZoneId).Distinct().ToList();
        var zones = await dbContext.TemperatureZones
            .AsNoTracking()
            .Where(z => zoneIds.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z.Kind.ToString(), cancellationToken)
            .ConfigureAwait(false);

        return new LotDetailDto(
            lot.ToDto(),
            balances.Select(b => b.ToDto(zones.GetValueOrDefault(b.ZoneId, "Unknown"))).ToList());
    }
}
