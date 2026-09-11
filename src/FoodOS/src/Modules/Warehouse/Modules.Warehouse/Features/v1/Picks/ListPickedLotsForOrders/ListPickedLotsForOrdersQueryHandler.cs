using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Picks.ListPickedLotsForOrders;

public sealed class ListPickedLotsForOrdersQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<ListPickedLotsForOrdersQuery, IReadOnlyList<PickedLotDto>>
{
    public async ValueTask<IReadOnlyList<PickedLotDto>> Handle(
        ListPickedLotsForOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.OrderIds.Count == 0)
        {
            return [];
        }

        var tasks = await dbContext.PickTasks
            .AsNoTracking()
            .Where(t =>
                query.OrderIds.Contains(t.OrderId)
                && t.Status == PickTaskStatus.Picked
                && t.LotId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tasks
            .Select(t => new PickedLotDto(
                t.OrderId,
                t.OrderLineId,
                t.ProductId,
                t.Zone,
                t.LotId!.Value,
                t.LotNo ?? string.Empty,
                t.Quantity))
            .ToList();
    }
}
