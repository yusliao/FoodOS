using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

public sealed record UpdateProductFulfillmentCommand(
    Guid ProductId,
    string TemperatureZone,
    int? ShelfLifeDays,
    int MinRemainingDaysOnShip,
    string BaseUom,
    bool CatchWeight,
    string? Barcode,
    string? StorageNote) : ICommand<Guid>;
