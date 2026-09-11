using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Routes;

public sealed record CreateRouteCommand(
    Guid WarehouseId,
    string Code,
    IReadOnlyList<Guid> StoreIds,
    Guid? DefaultVehicleId = null) : ICommand<Guid>;
