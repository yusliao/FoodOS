using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.LockOrdersForCutoff;

public sealed class LockOrdersForCutoffCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<LockOrdersForCutoffCommand, int>
{
    public async ValueTask<int> Handle(LockOrdersForCutoffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orders = await dbContext.SalesOrders
            .Where(o =>
                o.WarehouseId == command.WarehouseId
                && o.BusinessDate == command.BusinessDate
                && o.Status == SalesOrderStatus.Reserved)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var order in orders)
        {
            order.LockForCutoff();
        }

        if (orders.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return orders.Count;
    }
}
