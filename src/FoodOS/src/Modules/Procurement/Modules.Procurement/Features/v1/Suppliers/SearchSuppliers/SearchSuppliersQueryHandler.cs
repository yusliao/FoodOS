using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.SearchSuppliers;

public sealed class SearchSuppliersQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<SearchSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    public async ValueTask<IReadOnlyList<SupplierDto>> Handle(
        SearchSuppliersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Supplier> q = dbContext.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            q = q.Where(s =>
                EF.Functions.ILike(s.Code, $"%{term}%") ||
                EF.Functions.ILike(s.Name, $"%{term}%"));
        }

        var items = await q.OrderBy(s => s.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(s => s.ToDto()).ToList();
    }
}
