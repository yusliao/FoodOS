using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.StartOrderInTransit;

public sealed class StartOrderInTransitCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<StartOrderInTransitCommand, Guid>
{
    public async ValueTask<Guid> Handle(StartOrderInTransitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.MarkInTransit(command.Lots
            .Select(l => (l.OrderLineId, l.LotId, l.LotNo, l.Quantity))
            .ToList());

        foreach (var lot in order.Lines.SelectMany(l => l.Lots))
        {
            if (dbContext.Entry(lot).State == EntityState.Detached)
            {
                dbContext.SalesOrderLineLots.Add(lot);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
