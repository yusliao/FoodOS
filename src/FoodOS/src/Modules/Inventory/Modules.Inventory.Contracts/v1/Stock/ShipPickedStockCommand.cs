using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Move picked quantity onto the truck (Picked → InTransit). No HTTP; Logistics mediates.
/// </summary>
public sealed record ShipPickedStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? RefId = null) : ICommand<Guid>;
