using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class Route : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid WarehouseId { get; private set; }
    public string Code { get; private set; } = default!;
    public string StoreSequence { get; private set; } = default!;
    public Guid? DefaultVehicleId { get; private set; }

    private Route() { }

    public static Route Create(
        Guid warehouseId,
        string code,
        IReadOnlyList<Guid> storeIds,
        Guid? defaultVehicleId = null)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(storeIds);
        if (storeIds.Count == 0)
        {
            throw new ArgumentException("A route must visit at least one store.", nameof(storeIds));
        }

        if (storeIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Store ids must be valid.", nameof(storeIds));
        }

        return new Route
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            Code = code.Trim().ToUpperInvariant(),
            StoreSequence = string.Join(',', storeIds),
            DefaultVehicleId = defaultVehicleId
        };
    }

    public IReadOnlyList<Guid> GetStoreIds()
        => StoreSequence
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
}
