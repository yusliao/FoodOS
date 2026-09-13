using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.SearchShipments;

public sealed class SearchShipmentsQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<SearchShipmentsQuery, IReadOnlyList<ShipmentDto>>
{
    public async ValueTask<IReadOnlyList<ShipmentDto>> Handle(
        SearchShipmentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Shipments.AsNoTracking().Where(s => s.WarehouseId == query.WarehouseId);
        if (query.BusinessDate is { } date)
        {
            q = q.Where(s => s.BusinessDate == date);
        }

        var items = await q.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(s => s.ToDto()).ToList();
    }
}
