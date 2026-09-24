using System.Text.Json;

namespace FSH.Modules.WmsIntegration.Contracts.v1;

public enum WmsOperationKind
{
    Reserve,
    Release,
    Query,
    SubmitInboundOrder,
    SubmitOutboundOrder,
    CancelOutboundOrder,
}

public sealed record WmsOperationRequest(
    WmsOperationKind Kind,
    string IdempotencyKey,
    string CorrelationId,
    JsonElement Payload);

public sealed record WmsOperationResponse(
    string Status,
    string? ExternalOperationId,
    string? ErrorCode,
    string? Detail,
    JsonElement? Payload);

public interface IWmsStandardClient
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    Task<WmsOperationResponse> ExecuteAsync(WmsOperationRequest request, CancellationToken cancellationToken = default);
}
