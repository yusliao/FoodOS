using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrderById;

public sealed class GetPurchaseOrderByIdQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    public async ValueTask<PurchaseOrderDto> Handle(GetPurchaseOrderByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var po = await dbContext.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.PurchaseOrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {query.PurchaseOrderId} not found.");

        return po.ToDto();
    }
}
