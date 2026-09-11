using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Picks.GetMyPickTasks;

public sealed class GetMyPickTasksQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<GetMyPickTasksQuery, IReadOnlyList<PickTaskDto>>
{
    public async ValueTask<IReadOnlyList<PickTaskDto>> Handle(
        GetMyPickTasksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.PickTasks.AsNoTracking()
            .Where(t => t.Status == PickTaskStatus.Pending && t.LotId != null);
        if (query.WarehouseId is { } warehouseId && warehouseId != Guid.Empty)
        {
            q = q.Where(t => dbContext.Waves.Any(w => w.Id == t.WaveId && w.WarehouseId == warehouseId));
        }

        var tasks = await q.OrderBy(t => t.LotNo).ToListAsync(cancellationToken).ConfigureAwait(false);
        return tasks.Select(t => t.ToDto()).ToList();
    }
}
