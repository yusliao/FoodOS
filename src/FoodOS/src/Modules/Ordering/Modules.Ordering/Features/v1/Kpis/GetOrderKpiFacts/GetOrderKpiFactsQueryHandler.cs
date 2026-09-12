using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Kpis;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Kpis.GetOrderKpiFacts;

public sealed class GetOrderKpiFactsQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetOrderKpiFactsQuery, OrderKpiFactsDto>
{
    public async ValueTask<OrderKpiFactsDto> Handle(
        GetOrderKpiFactsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var orders = await dbContext.SalesOrders
            .AsNoTracking()
            .Where(o =>
                o.BusinessDate == query.Date
                && o.Status != SalesOrderStatus.Draft
                && o.Status != SalesOrderStatus.Cancelled)
            .Select(o => new
            {
                o.Status,
                OrderedQty = o.Lines.Sum(l => l.OrderedQty),
                ReservedQty = o.Lines.Sum(l => l.ReservedQty),
                DeliveredQty = o.Lines.Sum(l => l.DeliveredQty)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (orders.Count == 0)
        {
            return new OrderKpiFactsDto(0, 0, 0, 0, 0);
        }

        int fulfilled = orders.Count(o =>
            o.Status is SalesOrderStatus.Received or SalesOrderStatus.Reconciled);

        return new OrderKpiFactsDto(
            orders.Count,
            fulfilled,
            orders.Sum(o => o.OrderedQty),
            orders.Sum(o => o.ReservedQty),
            orders.Sum(o => o.DeliveredQty));
    }
}
