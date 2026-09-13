using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.SearchAfterSalesTickets;

public sealed class SearchAfterSalesTicketsQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<SearchAfterSalesTicketsQuery, IReadOnlyList<AfterSalesTicketDto>>
{
    public async ValueTask<IReadOnlyList<AfterSalesTicketDto>> Handle(
        SearchAfterSalesTicketsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.AfterSalesTickets.AsNoTracking().Where(t => t.StoreId == query.StoreId);
        if (query.OrderId is { } orderId && orderId != Guid.Empty)
        {
            q = q.Where(t => t.OrderId == orderId);
        }

        var items = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(t => t.ToDto()).ToList();
    }
}
