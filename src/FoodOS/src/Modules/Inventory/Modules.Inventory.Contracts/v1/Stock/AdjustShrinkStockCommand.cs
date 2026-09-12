using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Write off lot quantity (OnHand / Isolated). No HTTP; Warehouse mediates.
/// </summary>
public sealed record AdjustShrinkStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? RefId = null) : ICommand<Guid>;
