using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record PickAllocatedStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string IdempotencyKey,
    Guid? RefId = null) : ICommand<Guid>;
