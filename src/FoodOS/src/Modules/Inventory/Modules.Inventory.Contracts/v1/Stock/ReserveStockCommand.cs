using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record ReserveStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    decimal Quantity,
    Guid OrderId,
    string IdempotencyKey,
    Guid? OrderLineId = null) : ICommand<Guid>;
