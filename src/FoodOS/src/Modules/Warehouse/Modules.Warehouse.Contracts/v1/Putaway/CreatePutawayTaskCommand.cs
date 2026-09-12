using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Putaway;

public sealed record CreatePutawayTaskCommand(
    Guid WarehouseId,
    string Zone,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string Source = "QcPass",
    Guid? RefId = null) : ICommand<PutawayTaskDto>;
