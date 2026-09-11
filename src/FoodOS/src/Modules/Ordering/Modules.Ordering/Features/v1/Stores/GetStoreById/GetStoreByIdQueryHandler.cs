using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Stores.GetStoreById;

public sealed class GetStoreByIdQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetStoreByIdQuery, StoreDto>
{
    public async ValueTask<StoreDto> Handle(GetStoreByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var store = await dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {query.StoreId} not found.");

        return store.ToDto();
    }
}
