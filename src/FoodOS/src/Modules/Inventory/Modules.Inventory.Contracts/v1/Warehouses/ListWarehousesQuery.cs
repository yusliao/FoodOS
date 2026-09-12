using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Warehouses;

public sealed record ListWarehousesQuery : IQuery<IReadOnlyList<WarehouseDto>>;
