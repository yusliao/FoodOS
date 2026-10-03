using Mediator;

namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record GetWmsAvailabilityQuery(string Sku, string Uom) : IQuery<WmsProductAvailabilityDto>;

public sealed record WmsProductAvailabilityDto(string WarehouseCode, WmsAvailabilityResult Inventory);
