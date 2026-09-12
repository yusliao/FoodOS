using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.v1.Trace;
using LogisticsTrace = FSH.Modules.Logistics.Contracts.v1.Trace;
using Mediator;
using ProcurementTrace = FSH.Modules.Procurement.Contracts.v1.Trace;
using WarehouseTrace = FSH.Modules.Warehouse.Contracts.v1.Trace;

namespace FSH.Modules.Ops.Features.v1.Trace.GetLotTrace;

public sealed class GetLotTraceQueryHandler(IMediator mediator)
    : IQueryHandler<GetLotTraceQuery, LotTraceDto>
{
    public async ValueTask<LotTraceDto> Handle(GetLotTraceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var procurement = await mediator
            .Send(new ProcurementTrace.ListLotTraceEventsQuery(query.LotId), cancellationToken)
            .ConfigureAwait(false);
        var warehouse = await mediator
            .Send(new WarehouseTrace.ListLotTraceEventsQuery(query.LotId), cancellationToken)
            .ConfigureAwait(false);
        var logistics = await mediator
            .Send(new LogisticsTrace.ListLotTraceEventsQuery(query.LotId), cancellationToken)
            .ConfigureAwait(false);

        var events = procurement
            .Select(e => Map("Procurement", e.Id, e.ProductId, e.BizStep, e.Disposition, e.Quantity, e.Uom, e.SourceLocation, e.DestLocation, e.ActorUserId, e.OccurredAt, e.RefType, e.RefId, e.EvidenceUrl))
            .Concat(warehouse.Select(e => Map("Warehouse", e.Id, e.ProductId, e.BizStep, e.Disposition, e.Quantity, e.Uom, e.SourceLocation, e.DestLocation, e.ActorUserId, e.OccurredAt, e.RefType, e.RefId, e.EvidenceUrl)))
            .Concat(logistics.Select(e => Map("Logistics", e.Id, e.ProductId, e.BizStep, e.Disposition, e.Quantity, e.Uom, e.SourceLocation, e.DestLocation, e.ActorUserId, e.OccurredAt, e.RefType, e.RefId, e.EvidenceUrl)))
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToList();

        return new LotTraceDto(query.LotId, events);
    }

    private static LotTracePointDto Map(
        string module,
        Guid id,
        Guid productId,
        string bizStep,
        string disposition,
        decimal quantity,
        string uom,
        string? sourceLocation,
        string? destLocation,
        string actorUserId,
        DateTimeOffset occurredAt,
        string refType,
        Guid refId,
        string? evidenceUrl)
        => new(
            id,
            module,
            productId,
            bizStep,
            disposition,
            quantity,
            uom,
            sourceLocation,
            destLocation,
            actorUserId,
            occurredAt,
            refType,
            refId,
            evidenceUrl);
}
