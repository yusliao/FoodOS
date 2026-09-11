using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.GetOrderById;

public sealed class GetOrderByIdQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetOrderByIdQuery, SalesOrderDto>
{
    public async ValueTask<SalesOrderDto> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await dbContext.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {query.OrderId} not found.");

        return order.ToDto();
    }
}
