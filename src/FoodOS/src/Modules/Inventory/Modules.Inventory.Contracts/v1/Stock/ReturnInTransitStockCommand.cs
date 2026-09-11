using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Return rejected in-transit quantity to warehouse OnHand. No HTTP; Logistics mediates.
/// </summary>
public sealed record ReturnInTransitStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? RefId = null) : ICommand<Guid>;
