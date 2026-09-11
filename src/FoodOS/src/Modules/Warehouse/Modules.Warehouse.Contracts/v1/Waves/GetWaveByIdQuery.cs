using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Waves;

public sealed record GetWaveByIdQuery(Guid WaveId) : IQuery<WaveDto>;
