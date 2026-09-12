using FSH.Modules.Ops.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ops.Contracts.v1.Kpis;

public sealed record GetOpsKpisQuery(DateOnly? Date = null) : IQuery<OpsKpisDto>;
