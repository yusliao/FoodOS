using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Vehicles;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Vehicles.SearchVehicles;

public sealed class SearchVehiclesQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<SearchVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    public async ValueTask<IReadOnlyList<VehicleDto>> Handle(
        SearchVehiclesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var items = await dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Plate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(v => v.ToDto()).ToList();
    }
}
