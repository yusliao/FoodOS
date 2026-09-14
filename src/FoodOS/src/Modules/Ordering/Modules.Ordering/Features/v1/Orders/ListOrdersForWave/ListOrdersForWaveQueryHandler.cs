using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.ListOrdersForWave;

public sealed class ListOrdersForWaveQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<ListOrdersForWaveQuery, IReadOnlyList<SalesOrderDto>>
{
    public async ValueTask<IReadOnlyList<SalesOrderDto>> Handle(
        ListOrdersForWaveQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var orders = await dbContext.SalesOrders
            .AsNoTracking()
            .Where(o =>
                o.WarehouseId == query.WarehouseId
                && o.BusinessDate == query.BusinessDate
                && o.Status == SalesOrderStatus.Planned)
            .OrderBy(o => o.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var storeIds = orders.Select(o => o.StoreId).Distinct().ToList();
        var routeByStore = await dbContext.Stores
            .AsNoTracking()
            .Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.DefaultRouteId, cancellationToken)
            .ConfigureAwait(false);

        return orders
            .Select(o => o.ToDto(routeByStore.GetValueOrDefault(o.StoreId)))
            .ToList();
    }
}
