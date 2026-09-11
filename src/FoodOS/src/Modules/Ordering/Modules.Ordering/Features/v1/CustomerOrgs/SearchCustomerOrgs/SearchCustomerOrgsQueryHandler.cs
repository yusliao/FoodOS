using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.SearchCustomerOrgs;

public sealed class SearchCustomerOrgsQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<SearchCustomerOrgsQuery, IReadOnlyList<CustomerOrgDto>>
{
    public async ValueTask<IReadOnlyList<CustomerOrgDto>> Handle(
        SearchCustomerOrgsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<CustomerOrg> q = dbContext.CustomerOrgs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            q = q.Where(o =>
                EF.Functions.ILike(o.Code, $"%{term}%") ||
                EF.Functions.ILike(o.Name, $"%{term}%"));
        }

        var items = await q.OrderBy(o => o.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(o => o.ToDto()).ToList();
    }
}
