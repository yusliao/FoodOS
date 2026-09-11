using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record ReceiveInventoryCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    string LotNo,
    DateOnly ExpiryDate,
    decimal Quantity,
    string IdempotencyKey,
    DateOnly? ManufacturedOn = null,
    string? Origin = null) : ICommand<Guid>;
