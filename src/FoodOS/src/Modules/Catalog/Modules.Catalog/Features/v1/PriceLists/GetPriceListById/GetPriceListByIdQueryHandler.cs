using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Features.v1.PriceLists;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.GetPriceListById;

public sealed class GetPriceListByIdQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetPriceListByIdQuery, PriceListDto>
{
    public async ValueTask<PriceListDto> Handle(GetPriceListByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = await dbContext.PriceLists
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.PriceListId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Price list {query.PriceListId} not found.");

        return list.ToDto();
    }
}
