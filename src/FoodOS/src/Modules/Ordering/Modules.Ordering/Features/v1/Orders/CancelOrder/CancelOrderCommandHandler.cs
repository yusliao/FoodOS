using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;

public sealed class CancelOrderCommandHandler(OrderingDbContext dbContext, IMediator mediator, TimeProvider clock)
    : ICommandHandler<CancelOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        var holds = order.Lines
            .Where(l => l.ReservationId is not null)
            .Select(l => (l.Id, ReservationId: l.ReservationId!.Value))
            .ToList();
        int revision = order.Revision;

        // Validate cutoff and state before any independently committed inventory release.
        // Reservation identifiers were captured above; persist the order only after releases succeed.
        order.Cancel(clock.GetUtcNow());

        foreach (var (lineId, reservationId) in holds)
        {
            await InventoryStockOps.UnreserveAsync(
                    mediator,
                    reservationId,
                    order.Id,
                    lineId,
                    revision,
                    "cancel",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
