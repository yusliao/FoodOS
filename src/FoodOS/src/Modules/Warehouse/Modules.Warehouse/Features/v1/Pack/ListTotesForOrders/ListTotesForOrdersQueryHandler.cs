using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Pack;
using FSH.Modules.Warehouse.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Pack.ListTotesForOrders;

public sealed class ListTotesForOrdersQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<ListTotesForOrdersQuery, IReadOnlyList<PackedToteOrderDto>>
{
    public async ValueTask<IReadOnlyList<PackedToteOrderDto>> Handle(
        ListTotesForOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.OrderIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.PackToteOrders
            .AsNoTracking()
            .Where(o => query.OrderIds.Contains(o.OrderId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(o => new PackedToteOrderDto(o.OrderId, o.PackToteId)).ToList();
    }
}
