using FSH.Modules.Ops.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ops.Contracts.v1.Trace;

public sealed record GetLotTraceQuery(Guid LotId) : IQuery<LotTraceDto>;
