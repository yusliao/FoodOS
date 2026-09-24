using System.Net;
using System.Text;
using System.Text.Json;
using FSH.Modules.WmsIntegration;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Services;
using Microsoft.Extensions.Options;

namespace Integration.Tests.Tests.WmsIntegration;

public sealed class WmsStandardClientTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Send_Only_Standard_Payload_With_Idempotency_And_Signature()
    {
        var transport = new CaptureHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"accepted\",\"externalOperationId\":\"WMS-1\"}", Encoding.UTF8, "application/json"),
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://wms.example/") };
        var client = new WmsStandardClient(http, Options.Create(CreateOptions()));
        var payload = JsonSerializer.SerializeToElement(new { operationId = Guid.NewGuid(), orderId = Guid.NewGuid() });

        var result = await client.ExecuteAsync(new(WmsOperationKind.Reserve, "reserve:order:1", "corr-1", payload));

        result.Status.ShouldBe("accepted");
        transport.Path.ShouldBe("/api/v1/reservations");
        transport.IdempotencyKey.ShouldBe("reserve:order:1");
        using var sent = JsonDocument.Parse(transport.Body!);
        sent.RootElement.TryGetProperty("kind", out _).ShouldBeFalse();
        sent.RootElement.GetProperty("orderId").GetGuid().ShouldBe(payload.GetProperty("orderId").GetGuid());
        WmsSignature.Verify(transport.Timestamp!, transport.Body!, CreateOptions().SigningSecret, transport.Signature!).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_Unknown_On_Timeout()
    {
        using var http = new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://wms.example/") };
        var client = new WmsStandardClient(http, Options.Create(CreateOptions()));

        var result = await client.ExecuteAsync(new(
            WmsOperationKind.Query,
            "query:1",
            "corr-1",
            JsonSerializer.SerializeToElement(new { idempotencyKey = "reserve:order:1" })));

        result.Status.ShouldBe("unknown");
        result.ErrorCode.ShouldBe("timeout");
    }

    private static WmsIntegrationOptions CreateOptions() => new()
    {
        Enabled = true,
        Provider = "reference-wms",
        ConnectionId = "dev-primary",
        WarehouseId = "DC-01",
        BaseUrl = "https://wms.example/",
        SigningSecret = "client-test-signing-secret-32-bytes",
    };

    private sealed class CaptureHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Timestamp { get; private set; }
        public string? Signature { get; private set; }
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            Timestamp = request.Headers.GetValues("X-FoodOS-WMS-Timestamp").Single();
            Signature = request.Headers.GetValues("X-FoodOS-WMS-Signature").Single();
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return response;
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("simulated timeout");
    }
}
