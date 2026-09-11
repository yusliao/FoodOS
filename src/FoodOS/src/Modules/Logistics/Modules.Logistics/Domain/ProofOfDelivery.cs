using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class ProofOfDelivery : BaseEntity<Guid>
{
    public Guid StopId { get; private set; }
    public string SignedQtyJson { get; private set; } = default!;
    public string PhotoFileIds { get; private set; } = default!;
    public string SignerName { get; private set; } = default!;
    public string? Geo { get; private set; }
    public DateTimeOffset SignedAt { get; private set; }

    private ProofOfDelivery() { }

    internal static ProofOfDelivery Capture(
        Guid stopId,
        string signedQtyJson,
        IReadOnlyList<Guid> photoFileIds,
        string signerName,
        string? geo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedQtyJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(signerName);
        ArgumentNullException.ThrowIfNull(photoFileIds);

        return new ProofOfDelivery
        {
            Id = Guid.CreateVersion7(),
            StopId = stopId,
            SignedQtyJson = signedQtyJson.Trim(),
            PhotoFileIds = string.Join(',', photoFileIds),
            SignerName = signerName.Trim(),
            Geo = string.IsNullOrWhiteSpace(geo) ? null : geo.Trim(),
            SignedAt = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<Guid> GetPhotoIds()
        => string.IsNullOrWhiteSpace(PhotoFileIds)
            ? []
            : PhotoFileIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToList();
}
