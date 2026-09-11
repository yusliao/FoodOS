using FSH.Framework.Core.Domain;

namespace FSH.Modules.Warehouse.Domain;

public sealed class Location : AggregateRoot<Guid>
{
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string Code { get; private set; } = default!;
    public LocationType Type { get; private set; }

    private Location() { }

    public static Location Create(Guid warehouseId, Guid zoneId, string code, LocationType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        if (zoneId == Guid.Empty)
        {
            throw new ArgumentException("ZoneId is required.", nameof(zoneId));
        }

        return new Location
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            Code = code.Trim().ToUpperInvariant(),
            Type = type
        };
    }
}
