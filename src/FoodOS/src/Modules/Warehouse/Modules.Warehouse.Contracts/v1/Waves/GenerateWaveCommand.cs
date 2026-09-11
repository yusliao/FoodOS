using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Waves;

public sealed record GenerateWaveCommand(Guid WarehouseId, DateOnly? BusinessDate = null)
    : ICommand<IReadOnlyList<WaveDto>>;
