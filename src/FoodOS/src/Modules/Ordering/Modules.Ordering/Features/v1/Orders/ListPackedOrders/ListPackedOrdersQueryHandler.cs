using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.ListPackedOrders;

public sealed class ListPackedOrdersQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<ListPackedOrdersQuery, IReadOnlyList<SalesOrderDto>>
{
    public async ValueTask<IReadOnlyList<SalesOrderDto>> Handle(
        ListPackedOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.StoreIds.Count == 0)
        {
            return [];
        }

        var orders = await dbContext.SalesOrders
            .AsNoTracking()
            .Where(o =>
                o.WarehouseId == query.WarehouseId
                && o.BusinessDate == query.BusinessDate
                && o.Status == SalesOrderStatus.Packed
                && query.StoreIds.Contains(o.StoreId))
            .OrderBy(o => o.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return orders.Select(o => o.ToDto()).ToList();
    }
}
