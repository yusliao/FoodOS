using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Warehouses;

public sealed record CreateWarehouseCommand(
    string Code,
    string Name,
    string City,
    string? TimeZoneId = null) : ICommand<Guid>;
