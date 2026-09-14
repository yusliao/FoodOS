using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.SearchLotBalances;

public sealed class SearchLotBalancesQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<SearchLotBalancesQuery, PagedResponse<LotBalanceDto>>
{
    public async ValueTask<PagedResponse<LotBalanceDto>> Handle(
        SearchLotBalancesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        IQueryable<LotBalance> q = dbContext.LotBalances.AsNoTracking()
            .Where(b => b.WarehouseId == query.WarehouseId);

        if (query.ProductId is { } productId)
        {
            q = q.Where(b => b.ProductId == productId);
        }

        if (query.LotId is { } lotId)
        {
            q = q.Where(b => b.LotId == lotId);
        }

        if (query.Zone is { } zoneKind)
        {
            var zoneId = await dbContext.TemperatureZones
                .AsNoTracking()
                .Where(z => z.WarehouseId == query.WarehouseId && z.Kind == zoneKind)
                .Select(z => (Guid?)z.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (zoneId is null)
            {
                return EmptyPage(page, size);
            }

            q = q.Where(b => b.ZoneId == zoneId.Value);
        }

        q = q.OrderBy(b => b.ProductId).ThenBy(b => b.LotId);
        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        var zoneIds = items.Select(b => b.ZoneId).Distinct().ToList();
        var zones = await dbContext.TemperatureZones
            .AsNoTracking()
            .Where(z => zoneIds.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z.Kind.ToString(), cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<LotBalanceDto>
        {
            Items = items.Select(b => b.ToDto(zones.GetValueOrDefault(b.ZoneId, "Unknown"))).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size)
        };
    }

    private static PagedResponse<LotBalanceDto> EmptyPage(int page, int size)
        => new()
        {
            Items = [],
            PageNumber = page,
            PageSize = size,
            TotalCount = 0,
            TotalPages = 0
        };
}
