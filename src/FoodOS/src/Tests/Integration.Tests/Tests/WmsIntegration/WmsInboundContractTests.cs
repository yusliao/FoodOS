using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Services;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Integration.Tests.Tests.WmsIntegration;

[CollectionDefinition("WmsIntegration", DisableParallelization = true)]
public sealed class WmsIntegrationTestGroup;

[Collection("WmsIntegration")]
public sealed class WmsInboundContractTests(FshWebApplicationFactory factory) : IClassFixture<FshWebApplicationFactory>
{
    private const string Secret = "integration-wms-signing-secret-32bytes";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Signed_Events_Should_Deduplicate_Order_And_Recover_A_Gap()
    {
        using var configured = CreateConfiguredFactory(factory);
        using var client = configured.CreateClient();
        string objectId = $"OUT-{Guid.NewGuid():N}";

        var first = CreateEnvelope(objectId, $"event-{Guid.NewGuid():N}", 1);
        (await SendAsync(client, first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadReceiptAsync(await SendAsync(client, first))).Status.ShouldBe("duplicate");

        var stale = CreateEnvelope(objectId, $"event-{Guid.NewGuid():N}", 1);
        (await ReadReceiptAsync(await SendAsync(client, stale))).Status.ShouldBe("stale");

        var third = CreateEnvelope(objectId, $"event-{Guid.NewGuid():N}", 3);
        using var gap = await SendAsync(client, third);
        gap.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await ReadReceiptAsync(gap)).Status.ShouldBe("awaitingGap");

        var second = CreateEnvelope(objectId, $"event-{Guid.NewGuid():N}", 2);
        (await ReadReceiptAsync(await SendAsync(client, second))).Status.ShouldBe("accepted");
        var recovered = await ReadReceiptAsync(await SendAsync(client, third));
        recovered.Status.ShouldBe("accepted");
        recovered.LastAcceptedSequence.ShouldBe(3);
    }

    [Fact]
    public async Task Inbound_Endpoint_Should_Reject_Invalid_Or_Expired_Signatures()
    {
        using var configured = CreateConfiguredFactory(factory);
        using var client = configured.CreateClient();
        var envelope = CreateEnvelope($"OUT-{Guid.NewGuid():N}", $"event-{Guid.NewGuid():N}", 1);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

        using var invalid = CreateRequest(body, "0", "sha256=" + new string('0', 64));
        (await client.SendAsync(invalid)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        string expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);
        using var expired = CreateRequest(body, expiredTimestamp, WmsSignature.Sign(expiredTimestamp, body, Secret));
        (await client.SendAsync(expired)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var wrongConnection = envelope with { Provider = "another-wms", ExternalEventId = $"event-{Guid.NewGuid():N}" };
        (await SendAsync(client, wrongConnection)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static WebApplicationFactory<Program> CreateConfiguredFactory(FshWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WmsIntegration:Enabled"] = "true",
                ["WmsIntegration:Provider"] = "reference-wms",
                ["WmsIntegration:ConnectionId"] = "dev-primary",
                ["WmsIntegration:WarehouseId"] = "DC-01",
                ["WmsIntegration:Tenant"] = "root",
                ["WmsIntegration:BaseUrl"] = "https://wms.invalid/",
                ["WmsIntegration:SigningSecret"] = Secret,
                ["WmsIntegration:ReplayWindowSeconds"] = "300",
            })));

    private static WmsEventEnvelope CreateEnvelope(string objectId, string eventId, long sequence) => new(
        Guid.CreateVersion7(),
        "reference-wms",
        "dev-primary",
        "outbound.picked",
        "outboundOrder",
        "1.0",
        eventId,
        objectId,
        sequence,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        $"corr-{objectId}",
        null,
        $"reference-wms:dev-primary:{eventId}",
        JsonSerializer.SerializeToElement(new { outboundOrderId = objectId, pickedQuantity = 1 }));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, WmsEventEnvelope envelope)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request = CreateRequest(body, timestamp, WmsSignature.Sign(timestamp, body, Secret));
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(byte[] body, string timestamp, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wms/inbound/events")
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("tenant", "root");
        request.Headers.Add("X-FoodOS-WMS-Timestamp", timestamp);
        request.Headers.Add("X-FoodOS-WMS-Signature", signature);
        return request;
    }

    private static async Task<WmsEventReceipt> ReadReceiptAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<WmsEventReceipt>(await response.Content.ReadAsByteArrayAsync(), JsonOptions)
        ?? throw new InvalidOperationException("Missing WMS event receipt.");
}
