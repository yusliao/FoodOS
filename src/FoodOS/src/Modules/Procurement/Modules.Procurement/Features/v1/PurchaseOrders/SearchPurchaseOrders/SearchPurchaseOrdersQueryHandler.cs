using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.SearchPurchaseOrders;

public sealed class SearchPurchaseOrdersQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<SearchPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>
{
    public async ValueTask<IReadOnlyList<PurchaseOrderDto>> Handle(
        SearchPurchaseOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<PurchaseOrder> q = dbContext.PurchaseOrders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            q = q.Where(p => EF.Functions.ILike(p.Number, $"%{term}%"));
        }

        var items = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(p => p.ToDto()).ToList();
    }
}
