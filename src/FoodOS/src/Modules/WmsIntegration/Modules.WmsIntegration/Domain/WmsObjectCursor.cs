using FSH.Framework.Core.Domain;

namespace FSH.Modules.WmsIntegration.Domain;

public sealed class WmsObjectCursor : BaseEntity<Guid>
{
    public string Provider { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string ExternalObjectId { get; private set; } = default!;
    public long LastAcceptedSequence { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private WmsObjectCursor() { }

    public static WmsObjectCursor Create(string provider, string connectionId, string entityType, string externalObjectId, long sequence) => new()
    {
        Id = Guid.CreateVersion7(),
        Provider = provider,
        ConnectionId = connectionId,
        EntityType = entityType,
        ExternalObjectId = externalObjectId,
        LastAcceptedSequence = sequence,
        UpdatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
    };

    public void Advance(long sequence)
    {
        LastAcceptedSequence = sequence;
        UpdatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
    }
}
