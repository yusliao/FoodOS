using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Routes.SearchRoutes;

public sealed class SearchRoutesQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<SearchRoutesQuery, IReadOnlyList<RouteDto>>
{
    public async ValueTask<IReadOnlyList<RouteDto>> Handle(
        SearchRoutesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var items = await dbContext.Routes
            .AsNoTracking()
            .Where(r => r.WarehouseId == query.WarehouseId)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(r => r.ToDto()).ToList();
    }
}
