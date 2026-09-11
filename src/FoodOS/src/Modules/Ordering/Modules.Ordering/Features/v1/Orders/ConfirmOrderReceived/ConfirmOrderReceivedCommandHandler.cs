using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.ConfirmOrderReceived;

public sealed class ConfirmOrderReceivedCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<ConfirmOrderReceivedCommand, Guid>
{
    public async ValueTask<Guid> Handle(ConfirmOrderReceivedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.MarkReceived(command.Receipts
            .Select(r => (r.OrderLineId, r.LotId, r.DeliveredQty, r.ReturnedQty, r.VarianceReason))
            .ToList());

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
