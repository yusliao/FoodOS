using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Cycle-count against lot ATP (<c>Available</c>). Gain receives onto OnHand; loss writes down Available only.
/// </summary>
public sealed record AdjustCountStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal CountedAvailable,
    string IdempotencyKey) : ICommand<Guid>;
