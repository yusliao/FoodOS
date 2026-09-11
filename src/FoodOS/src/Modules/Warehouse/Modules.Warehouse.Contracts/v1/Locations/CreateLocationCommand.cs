using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Locations;

public sealed record CreateLocationCommand(
    Guid WarehouseId,
    Guid ZoneId,
    string Code,
    string Type) : ICommand<Guid>;
