using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Trace;
using FSH.Modules.Warehouse.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Trace.ListLotTraceEvents;

public sealed class ListLotTraceEventsQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<ListLotTraceEventsQuery, IReadOnlyList<LotTraceEventDto>>
{
    public async ValueTask<IReadOnlyList<LotTraceEventDto>> Handle(
        ListLotTraceEventsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await dbContext.TraceEvents
            .AsNoTracking()
            .Where(e => e.LotId == query.LotId)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .Select(e => new LotTraceEventDto(
                e.Id,
                e.LotId!.Value,
                e.ProductId,
                e.BizStep,
                e.Disposition,
                e.Quantity,
                e.Uom,
                e.SourceLocation,
                e.DestLocation,
                e.ActorUserId,
                e.OccurredAt,
                e.RefType,
                e.RefId,
                EvidenceUrl: null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
