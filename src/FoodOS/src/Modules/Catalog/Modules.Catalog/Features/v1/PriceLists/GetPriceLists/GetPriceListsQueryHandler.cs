using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using FSH.Modules.Catalog.Features.v1.PriceLists;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.GetPriceLists;

public sealed class GetPriceListsQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetPriceListsQuery, IReadOnlyList<PriceListDto>>
{
    public async ValueTask<IReadOnlyList<PriceListDto>> Handle(GetPriceListsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<PriceList> lists = dbContext.PriceLists.AsNoTracking();
        if (query.CustomerOrgId is { } orgId && orgId != Guid.Empty)
        {
            lists = lists.Where(l => l.CustomerOrgId == orgId);
        }

        if (query.CustomerLevelId is { } levelId && levelId != Guid.Empty)
        {
            lists = lists.Where(l => l.CustomerLevelId == levelId);
        }

        var items = await lists
            .OrderByDescending(l => l.Priority)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await items.ToDtosAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }
}
