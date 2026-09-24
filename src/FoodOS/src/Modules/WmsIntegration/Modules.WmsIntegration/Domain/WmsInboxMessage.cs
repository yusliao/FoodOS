using FSH.Framework.Core.Domain;

namespace FSH.Modules.WmsIntegration.Domain;

public sealed class WmsInboxMessage : BaseEntity<Guid>
{
    public string Provider { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string EventType { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string ExternalEventId { get; private set; } = default!;
    public string ExternalObjectId { get; private set; } = default!;
    public long Sequence { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public string CorrelationId { get; private set; } = default!;
    public string SchemaVersion { get; private set; } = default!;
    public string RawPayload { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public string? Detail { get; private set; }

    private WmsInboxMessage() { }

    public static WmsInboxMessage Create(
        Guid id,
        string provider,
        string connectionId,
        string eventType,
        string entityType,
        string externalEventId,
        string externalObjectId,
        long sequence,
        string idempotencyKey,
        string correlationId,
        string schemaVersion,
        string rawPayload,
        string status,
        DateTimeOffset occurredAt,
        string? detail) => new()
        {
            Id = id,
            Provider = provider,
            ConnectionId = connectionId,
            EventType = eventType,
            EntityType = entityType,
            ExternalEventId = externalEventId,
            ExternalObjectId = externalObjectId,
            Sequence = sequence,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            SchemaVersion = schemaVersion,
            RawPayload = rawPayload,
            Status = status,
            OccurredAtUtc = occurredAt.UtcDateTime,
            ReceivedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
            Detail = detail,
        };

    public void AcceptAfterGap()
    {
        Status = "accepted";
        Detail = null;
    }
}
