using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Locations;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Locations.SearchLocations;

public sealed class SearchLocationsQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<SearchLocationsQuery, IReadOnlyList<LocationDto>>
{
    public async ValueTask<IReadOnlyList<LocationDto>> Handle(
        SearchLocationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Locations.AsNoTracking().Where(l => l.WarehouseId == query.WarehouseId);
        if (query.ZoneId is { } zoneId && zoneId != Guid.Empty)
        {
            q = q.Where(l => l.ZoneId == zoneId);
        }

        var items = await q.OrderBy(l => l.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(l => l.ToDto()).ToList();
    }
}
