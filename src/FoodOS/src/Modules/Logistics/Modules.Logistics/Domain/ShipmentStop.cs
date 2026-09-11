using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Logistics.Domain;

public sealed class ShipmentStop : BaseEntity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public Guid StoreId { get; private set; }
    public int Sequence { get; private set; }
    public string? Window { get; private set; }
    public StopStatus Status { get; private set; }
    public ProofOfDelivery? ProofOfDelivery { get; private set; }

    private ShipmentStop() { }

    internal static ShipmentStop Create(Guid shipmentId, Guid storeId, int sequence, string? window)
    {
        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("StoreId is required.", nameof(storeId));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Stop sequence must be at least 1.");
        }

        return new ShipmentStop
        {
            Id = Guid.CreateVersion7(),
            ShipmentId = shipmentId,
            StoreId = storeId,
            Sequence = sequence,
            Window = string.IsNullOrWhiteSpace(window) ? null : window.Trim(),
            Status = StopStatus.Pending
        };
    }

    internal ProofOfDelivery ConfirmPod(
        string signedQtyJson,
        IReadOnlyList<Guid> photoFileIds,
        string signerName,
        string? geo)
    {
        if (Status == StopStatus.Delivered && ProofOfDelivery is not null)
        {
            return ProofOfDelivery;
        }

        if (Status != StopStatus.Pending)
        {
            throw new CustomException(
                "Only pending stops can be signed.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var pod = global::FSH.Modules.Logistics.Domain.ProofOfDelivery.Capture(
            Id, signedQtyJson, photoFileIds, signerName, geo);
        ProofOfDelivery = pod;
        Status = StopStatus.Delivered;
        return pod;
    }
}
