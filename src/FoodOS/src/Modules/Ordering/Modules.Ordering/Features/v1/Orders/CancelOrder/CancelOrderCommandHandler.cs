using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;

public sealed class CancelOrderCommandHandler(OrderingDbContext dbContext, TimeProvider clock)
    : ICommandHandler<CancelOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        DateTimeOffset utcNow = clock.GetUtcNow();
        order.Cancel(utcNow);
        order.MarkWarehouseNotificationPending("Order cancellation accepted by FoodOS; warehouse confirmation is pending.", utcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
