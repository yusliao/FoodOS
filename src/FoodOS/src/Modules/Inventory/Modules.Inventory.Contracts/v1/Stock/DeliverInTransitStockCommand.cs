using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Confirm POD quantity that left the company (InTransit decrease). No HTTP; Logistics mediates.
/// </summary>
public sealed record DeliverInTransitStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? RefId = null) : ICommand<Guid>;
