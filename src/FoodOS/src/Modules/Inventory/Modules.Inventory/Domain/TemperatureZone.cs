using FSH.Framework.Core.Domain;
using FSH.Modules.Inventory.Contracts;

namespace FSH.Modules.Inventory.Domain;

public sealed class TemperatureZone : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid WarehouseId { get; private set; }
    public string Code { get; private set; } = default!;
    public TemperatureZoneKind Kind { get; private set; }

    private TemperatureZone() { }

    public static TemperatureZone Create(Guid warehouseId, TemperatureZoneKind kind)
    {
        return new TemperatureZone
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            Kind = kind,
            Code = kind.ToString().ToUpperInvariant()
        };
    }
}
