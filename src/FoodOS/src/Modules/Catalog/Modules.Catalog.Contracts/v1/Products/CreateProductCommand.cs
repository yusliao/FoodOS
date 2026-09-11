using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    Guid BrandId,
    Guid CategoryId,
    decimal PriceAmount,
    string PriceCurrency = "USD",
    int Stock = 0,
    string TemperatureZone = "Ambient",
    int? ShelfLifeDays = null,
    int MinRemainingDaysOnShip = 0,
    string BaseUom = "EA",
    bool CatchWeight = false,
    string? Barcode = null,
    string? StorageNote = null) : ICommand<Guid>;
