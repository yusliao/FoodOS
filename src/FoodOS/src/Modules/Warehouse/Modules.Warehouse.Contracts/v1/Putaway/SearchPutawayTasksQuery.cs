using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Putaway;

public sealed record SearchPutawayTasksQuery(Guid WarehouseId, string? Status = null)
    : IQuery<IReadOnlyList<PutawayTaskDto>>;
