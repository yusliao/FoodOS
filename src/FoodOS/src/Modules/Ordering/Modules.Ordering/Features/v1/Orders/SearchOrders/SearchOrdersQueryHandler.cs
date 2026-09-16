using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;

public sealed class SearchOrdersQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<SearchOrdersQuery, PagedResponse<SalesOrderDto>>
{
    public async ValueTask<PagedResponse<SalesOrderDto>> Handle(
        SearchOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        IQueryable<SalesOrder> q = dbContext.SalesOrders.AsNoTracking();
        if (query.StoreId is { } storeId && storeId != Guid.Empty)
        {
            q = q.Where(o => o.StoreId == storeId);
        }

        if (query.Status is not null)
        {
            var status = Enum.Parse<SalesOrderStatus>(query.Status, ignoreCase: true);
            q = q.Where(order => order.Status == status);
        }

        q = q.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);
        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<SalesOrderDto>
        {
            Items = items.Select(o => o.ToDto()).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size)
        };
    }
}
