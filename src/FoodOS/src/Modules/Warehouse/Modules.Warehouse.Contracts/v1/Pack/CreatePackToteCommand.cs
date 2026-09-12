using FSH.Modules.Warehouse.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Pack;

public sealed record CreatePackToteCommand(
    Guid WaveId,
    IReadOnlyList<Guid> OrderIds,
    string? Sscc = null,
    Guid? DockLocationId = null) : ICommand<PackToteDto>;
