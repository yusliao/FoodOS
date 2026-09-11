using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Stores;

public sealed record CreateStoreCommand(
    Guid CustomerOrgId,
    string Code,
    string Name,
    string Address,
    Guid DefaultWarehouseId,
    Guid? DefaultRouteId = null,
    string? DeliveryWindow = null) : ICommand<Guid>;
