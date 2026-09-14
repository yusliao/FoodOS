using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Drivers;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Drivers.SearchDrivers;

public sealed class SearchDriversQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<SearchDriversQuery, IReadOnlyList<DriverDto>>
{
    public async ValueTask<IReadOnlyList<DriverDto>> Handle(
        SearchDriversQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var items = await dbContext.Drivers
            .AsNoTracking()
            .OrderBy(d => d.Phone)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(d => d.ToDto()).ToList();
    }
}
