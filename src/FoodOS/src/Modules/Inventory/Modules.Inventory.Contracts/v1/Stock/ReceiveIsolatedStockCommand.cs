using FSH.Modules.Inventory.Contracts;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Create or reuse a lot and put quantity into Isolated in one step.
/// ATP is unchanged; the lot is marked Isolated when fully quarantined.
/// </summary>
public sealed record ReceiveIsolatedStockCommand(
    Guid WarehouseId,
    TemperatureZoneKind Zone,
    Guid ProductId,
    string LotNo,
    DateOnly ExpiryDate,
    decimal Quantity,
    string IdempotencyKey,
    DateOnly? ManufacturedOn = null,
    string? Origin = null,
    Guid? SupplierId = null) : ICommand<Guid>;
