using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Kpis;

public sealed record GetOrderKpiFactsQuery(DateOnly Date) : IQuery<OrderKpiFactsDto>;
