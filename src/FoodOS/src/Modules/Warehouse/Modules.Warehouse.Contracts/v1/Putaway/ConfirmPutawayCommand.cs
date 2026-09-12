using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Putaway;

public sealed record ConfirmPutawayCommand(Guid PutawayTaskId, Guid LocationId) : ICommand<PutawayTaskDto>;
