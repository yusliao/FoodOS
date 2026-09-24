using FSH.Framework.Core.Domain;
using FSH.Modules.WmsIntegration.Contracts.v1;

namespace FSH.Modules.WmsIntegration.Domain;

public sealed class WmsOutboundOperation : BaseEntity<Guid>
{
    public string Provider { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string Kind { get; private set; } = default!;
    public string IdempotencyKey { get; private set; } = default!;
    public string CorrelationId { get; private set; } = default!;
    public string RequestHash { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public string Status { get; private set; } = "pending";
    public string? ExternalOperationId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WmsOutboundOperation() { }

    public static WmsOutboundOperation Create(
        Guid id,
        string provider,
        string connectionId,
        string kind,
        string idempotencyKey,
        string correlationId,
        string requestHash,
        string payloadJson,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        return new()
        {
            Id = id,
            Provider = provider.Trim().ToUpperInvariant(),
            ConnectionId = connectionId.Trim().ToUpperInvariant(),
            Kind = kind,
            IdempotencyKey = idempotencyKey.Trim(),
            CorrelationId = correlationId.Trim(),
            RequestHash = requestHash,
            PayloadJson = payloadJson,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public void Record(WmsOperationResponse response, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);
        Status = response.Status.Trim().ToUpperInvariant() switch
        {
            "ACCEPTED" => "accepted",
            "COMPLETED" => "completed",
            "REJECTED" => "rejected",
            _ => "unknown",
        };
        ExternalOperationId = response.ExternalOperationId;
        ErrorCode = response.ErrorCode;
        Detail = response.Detail;
        UpdatedAt = utcNow;
    }
}
