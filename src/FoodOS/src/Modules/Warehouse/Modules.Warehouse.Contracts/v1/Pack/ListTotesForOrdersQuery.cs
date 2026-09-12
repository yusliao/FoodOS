using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Pack;

public sealed record ListTotesForOrdersQuery(IReadOnlyList<Guid> OrderIds)
    : IQuery<IReadOnlyList<PackedToteOrderDto>>;
