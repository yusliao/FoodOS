using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SendPurchaseOrder;

public sealed class SendPurchaseOrderCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<SendPurchaseOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(SendPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var po = await dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PurchaseOrderId} not found.");

        po.Send();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return po.Id;
    }
}
