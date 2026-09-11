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

        var items = await lists
            .OrderByDescending(l => l.Priority)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items.Select(l => l.ToDto()).ToList();
    }
}
