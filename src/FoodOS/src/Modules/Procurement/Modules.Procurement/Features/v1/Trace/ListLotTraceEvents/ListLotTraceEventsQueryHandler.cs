using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Trace;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Trace.ListLotTraceEvents;

public sealed class ListLotTraceEventsQueryHandler(ProcurementDbContext dbContext)
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
                e.EvidenceUrl))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
