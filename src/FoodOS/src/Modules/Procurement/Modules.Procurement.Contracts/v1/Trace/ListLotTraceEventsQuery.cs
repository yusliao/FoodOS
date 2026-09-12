using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Trace;

public sealed record ListLotTraceEventsQuery(Guid LotId)
    : IQuery<IReadOnlyList<LotTraceEventDto>>;
