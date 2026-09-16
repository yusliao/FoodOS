using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.ReconcileOrder;

public sealed class ReconcileOrderCommandHandler(OrderingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<ReconcileOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReconcileOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.Equals(currentUser.GetTenant(), MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Operational reconciliation requires an operator identity.");
        }

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.Reconcile();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }
}
