using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record IsolateStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    string? Reason = null) : ICommand<Guid>;
