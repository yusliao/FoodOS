using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Picks;

public sealed record ListPickedLotsForOrdersQuery(IReadOnlyList<Guid> OrderIds)
    : IQuery<IReadOnlyList<PickedLotDto>>;
