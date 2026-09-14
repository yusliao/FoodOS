using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Routes;

public sealed record SearchRoutesQuery(Guid WarehouseId) : IQuery<IReadOnlyList<RouteDto>>;
