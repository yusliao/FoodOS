using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsStandardClient(HttpClient httpClient, IOptions<WmsIntegrationOptions> options) : IWmsStandardClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WmsOperationResponse> ExecuteAsync(WmsOperationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new("unknown", null, "wms_not_configured", "The WMS connection is not configured.", null);
        }

        string path = request.Kind switch
        {
            WmsOperationKind.Reserve => "api/v1/reservations",
            WmsOperationKind.Release => "api/v1/reservations/release",
            WmsOperationKind.Query => "api/v1/operations/query",
            WmsOperationKind.SubmitInboundOrder => "api/v1/inbound-orders",
            WmsOperationKind.SubmitOutboundOrder => "api/v1/outbound-orders",
            WmsOperationKind.CancelOutboundOrder => "api/v1/outbound-orders/cancel",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported WMS operation."),
        };

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(request.Payload, JsonOptions);
        string timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        message.Content.Headers.ContentType = new("application/json");
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        message.Headers.Add("X-Correlation-Id", request.CorrelationId);
        message.Headers.Add("X-FoodOS-WMS-Timestamp", timestamp);
        message.Headers.Add("X-FoodOS-WMS-Signature", WmsSignature.Sign(timestamp, body, settings.SigningSecret));

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WmsOperationResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? new("unknown", null, "empty_response", "WMS returned an empty response.", null);
            }

            string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            bool unknown = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;
            return new(
                unknown ? "unknown" : "rejected",
                null,
                $"http_{(int)response.StatusCode}",
                detail,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new("unknown", null, "timeout", "WMS result is unknown; query with the same idempotency key.", null);
        }
        catch (HttpRequestException ex)
        {
            return new("unknown", null, "transport_error", ex.Message, null);
        }
    }
}
