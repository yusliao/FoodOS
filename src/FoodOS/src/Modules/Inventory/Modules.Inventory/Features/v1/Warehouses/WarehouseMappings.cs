using System.Globalization;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Domain;

namespace FSH.Modules.Inventory.Features.v1.Warehouses;

internal static class WarehouseMappings
{
    public static WarehouseDto ToDto(this Warehouse warehouse)
        => new(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.City,
            warehouse.TimeZoneId,
            new OperatingClockDto(
                warehouse.Clock.CutoffLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                warehouse.Clock.LoadLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                warehouse.Clock.DeliverFromLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                warehouse.Clock.DeliverToLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                warehouse.Clock.ReconcileLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                warehouse.Clock.TimeZoneId),
            warehouse.Zones.Select(z => new TemperatureZoneDto(z.Id, z.Code, z.Kind.ToString())).ToList(),
            warehouse.CreatedAtUtc);
}
