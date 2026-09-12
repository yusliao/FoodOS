using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Shrinkage;

public sealed record CreateShrinkageCommand(
    Guid WarehouseId,
    string Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string Reason,
    IReadOnlyList<Guid>? PhotoFileIds = null) : ICommand<ShrinkageDto>;
