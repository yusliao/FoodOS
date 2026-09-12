using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Trace;

public sealed record ListLotTraceEventsQuery(Guid LotId)
    : IQuery<IReadOnlyList<LotTraceEventDto>>;
