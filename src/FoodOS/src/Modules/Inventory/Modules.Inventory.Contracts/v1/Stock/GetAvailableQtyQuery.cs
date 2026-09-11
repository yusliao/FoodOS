using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record GetAvailableQtyQuery(
    Guid WarehouseId,
    Guid ProductId,
    TemperatureZoneKind? Zone = null) : IQuery<AvailableQtyDto>;
