using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Locations;

public sealed record SearchLocationsQuery(Guid WarehouseId, Guid? ZoneId = null)
    : IQuery<IReadOnlyList<LocationDto>>;
