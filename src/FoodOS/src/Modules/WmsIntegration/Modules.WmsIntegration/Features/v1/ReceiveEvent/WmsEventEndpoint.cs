using System.Text;
using System.Text.Json;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Features.v1.ReceiveEvent;

public static class ReceiveWmsEventEndpoint
{
    private const int MaximumBodyBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static RouteHandlerBuilder MapWmsEventEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/events", ReceiveAsync)
            .WithName("ReceiveWmsEvent")
            .WithSummary("Receive a signed FoodOS WMS Standard v1 event")
            .AllowAnonymous();
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        IOptions<WmsIntegrationOptions> options,
        WmsInboxService inbox,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (!string.Equals(request.Headers["tenant"], settings.Tenant, StringComparison.Ordinal)) return Results.Unauthorized();
        if (request.ContentLength > MaximumBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        if (memory.Length == 0 || memory.Length > MaximumBodyBytes) return Results.BadRequest(new { error = "invalid_body" });
        byte[] body = memory.ToArray();

        string timestamp = request.Headers["X-FoodOS-WMS-Timestamp"].ToString();
        string signature = request.Headers["X-FoodOS-WMS-Signature"].ToString();
        if (!WmsSignature.IsWithinReplayWindow(timestamp, TimeProvider.System, settings.ReplayWindowSeconds)
            || string.IsNullOrWhiteSpace(signature)
            || !WmsSignature.Verify(timestamp, body, settings.SigningSecret, signature))
        {
            return Results.Unauthorized();
        }

        WmsEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WmsEventEnvelope>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        if (envelope is null
            || envelope.MessageId == Guid.Empty
            || envelope.Sequence <= 0
            || envelope.SchemaVersion != "1.0"
            || string.IsNullOrWhiteSpace(envelope.EventType)
            || envelope.EventType.Length > 120
            || string.IsNullOrWhiteSpace(envelope.EntityType)
            || envelope.EntityType.Length > 80
            || string.IsNullOrWhiteSpace(envelope.ExternalEventId)
            || envelope.ExternalEventId.Length > 160
            || string.IsNullOrWhiteSpace(envelope.ExternalObjectId)
            || envelope.ExternalObjectId.Length > 160
            || string.IsNullOrWhiteSpace(envelope.IdempotencyKey)
            || envelope.IdempotencyKey.Length > 200
            || string.IsNullOrWhiteSpace(envelope.CorrelationId)
            || envelope.CorrelationId.Length > 160
            || envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            return Results.BadRequest(new { error = "invalid_envelope" });
        }
        if (!string.Equals(envelope.Provider, settings.Provider, StringComparison.Ordinal)
            || !string.Equals(envelope.ConnectionId, settings.ConnectionId, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }
        if (!WmsEventPayloadValidator.TryValidate(envelope, out string payloadError))
        {
            return Results.BadRequest(new { error = "invalid_event_payload", detail = payloadError });
        }

        var receipt = await inbox.ReceiveAsync(envelope, Encoding.UTF8.GetString(body), cancellationToken).ConfigureAwait(false);
        return receipt.Status == "awaitingGap" ? Results.Accepted(value: receipt) : Results.Ok(receipt);
    }
}
