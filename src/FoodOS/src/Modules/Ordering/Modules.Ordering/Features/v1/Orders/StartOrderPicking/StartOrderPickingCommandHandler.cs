using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.StartOrderPicking;

public sealed class StartOrderPickingCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<StartOrderPickingCommand, Guid>
{
    public async ValueTask<Guid> Handle(StartOrderPickingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.StartPicking();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
