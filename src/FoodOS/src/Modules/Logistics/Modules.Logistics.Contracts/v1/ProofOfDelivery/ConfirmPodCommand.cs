using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;

public sealed record PodSignedLine(Guid OrderLineId, Guid LotId, decimal SignedQty);

public sealed record ConfirmPodCommand(
    Guid StopId,
    IReadOnlyList<PodSignedLine> Lines,
    string SignerName,
    IReadOnlyList<Guid>? PhotoFileIds = null,
    string? Geo = null) : ICommand<ShipmentDto>;
