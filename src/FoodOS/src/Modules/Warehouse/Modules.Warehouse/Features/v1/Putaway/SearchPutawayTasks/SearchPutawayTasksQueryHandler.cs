using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.SearchPutawayTasks;

public sealed class SearchPutawayTasksQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<SearchPutawayTasksQuery, IReadOnlyList<PutawayTaskDto>>
{
    public async ValueTask<IReadOnlyList<PutawayTaskDto>> Handle(
        SearchPutawayTasksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.PutawayTasks.AsNoTracking().Where(t => t.WarehouseId == query.WarehouseId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<PutawayTaskStatus>(query.Status, ignoreCase: true, out var status))
            {
                throw new CustomException(
                    $"Unknown putaway status '{query.Status}'.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            }

            q = q.Where(t => t.Status == status);
        }

        var items = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return items.Select(t => t.ToDto()).ToList();
    }
}
