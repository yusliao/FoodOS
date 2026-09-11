using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Stores.GetStores;

public sealed class GetStoresQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetStoresQuery, IReadOnlyList<StoreDto>>
{
    public async ValueTask<IReadOnlyList<StoreDto>> Handle(GetStoresQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Store> q = dbContext.Stores.AsNoTracking();
        if (query.CustomerOrgId is { } orgId && orgId != Guid.Empty)
        {
            q = q.Where(s => s.CustomerOrgId == orgId);
        }

        var items = await q.OrderBy(s => s.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(s => s.ToDto()).ToList();
    }
}
