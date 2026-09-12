using FSH.Modules.Warehouse.Contracts.v1.Pack;
using FSH.Modules.Warehouse.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Pack.ListOrdersForTotes;

public sealed class ListOrdersForTotesQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<ListOrdersForTotesQuery, IReadOnlyList<Guid>>
{
    public async ValueTask<IReadOnlyList<Guid>> Handle(
        ListOrdersForTotesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ToteIds.Count == 0)
        {
            return [];
        }

        return await dbContext.PackToteOrders
            .AsNoTracking()
            .Where(o => query.ToteIds.Contains(o.PackToteId))
            .Select(o => o.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
