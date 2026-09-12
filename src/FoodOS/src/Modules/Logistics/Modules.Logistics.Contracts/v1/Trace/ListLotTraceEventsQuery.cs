using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Trace;

public sealed record ListLotTraceEventsQuery(Guid LotId)
    : IQuery<IReadOnlyList<LotTraceEventDto>>;
