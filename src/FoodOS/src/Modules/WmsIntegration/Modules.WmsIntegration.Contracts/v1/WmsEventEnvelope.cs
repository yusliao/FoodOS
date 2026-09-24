using System.Text.Json;

namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record WmsEventEnvelope(
    Guid MessageId,
    string Provider,
    string ConnectionId,
    string EventType,
    string EntityType,
    string SchemaVersion,
    string ExternalEventId,
    string ExternalObjectId,
    long Sequence,
    DateTimeOffset OccurredAt,
    DateTimeOffset SentAt,
    string CorrelationId,
    string? CausationId,
    string IdempotencyKey,
    JsonElement Payload);

public sealed record WmsEventReceipt(
    Guid MessageId,
    string Status,
    long? LastAcceptedSequence,
    string? Detail);
