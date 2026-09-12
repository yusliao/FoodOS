using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Shipments;

public sealed record LoadShipmentCommand(
    Guid ShipmentId,
    IReadOnlyList<Guid>? OrderIds = null,
    IReadOnlyList<Guid>? ToteIds = null) : ICommand<ShipmentDto>;
