using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Cutoff;

public sealed record ConfirmCutoffCommand(Guid WarehouseId, DateOnly? BusinessDate = null) : ICommand<CutoffResultDto>;
