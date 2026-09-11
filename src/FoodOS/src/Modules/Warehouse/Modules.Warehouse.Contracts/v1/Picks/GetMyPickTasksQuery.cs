using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Picks;

public sealed record GetMyPickTasksQuery(Guid? WarehouseId = null) : IQuery<IReadOnlyList<PickTaskDto>>;
