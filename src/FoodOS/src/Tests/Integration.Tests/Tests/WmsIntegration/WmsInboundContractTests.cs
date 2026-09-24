using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Services;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task Accepted_Inventory_Events_Should_Project_Availability_In_Sequence()
    {
        using var configured = CreateConfiguredFactory(factory);
        using var client = configured.CreateClient();
        string objectId = $"INV-{Guid.NewGuid():N}";
        string sku = $"SKU-{Guid.NewGuid():N}";

        (await ReadReceiptAsync(await SendAsync(client,
            CreateInventoryEnvelope(objectId, $"event-{Guid.NewGuid():N}", 1, sku, 8m)))).Status.ShouldBe("accepted");
        await AssertAvailabilityAsync(configured, sku, 8m, 8m, true);

        var third = CreateInventoryEnvelope(objectId, $"event-{Guid.NewGuid():N}", 3, sku, 4m);
        using var gap = await SendAsync(client, third);
        gap.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var gapEnvelope = await ReadReceiptAsync(gap);
        gapEnvelope.Status.ShouldBe("awaitingGap");
        await AssertAvailabilityAsync(configured, sku, 8m, 8m, true);

        (await ReadReceiptAsync(await SendAsync(client,
            CreateInventoryEnvelope(objectId, $"event-{Guid.NewGuid():N}", 2, sku, 6m)))).Status.ShouldBe("accepted");
        await AssertAvailabilityAsync(configured, sku, 6m, 6m, true);

        using var recoveredThird = await SendAsync(client, third);
        (await ReadReceiptAsync(recoveredThird)).Status.ShouldBe("accepted");
        await AssertAvailabilityAsync(configured, sku, 4m, 5m, false);

        string staleSku = $"SKU-{Guid.NewGuid():N}";
        (await ReadReceiptAsync(await SendAsync(client, CreateInventoryEnvelope(
            $"INV-{Guid.NewGuid():N}",
            $"event-{Guid.NewGuid():N}",
            1,
            staleSku,
            10m,
            DateTimeOffset.UtcNow.AddMinutes(-10))))).Status.ShouldBe("accepted");
        await AssertAvailabilityAsync(configured, staleSku, 10m, 1m, false);
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

    [Fact]
    public async Task Inbound_Endpoint_Should_Reject_Unknown_Or_Incomplete_Event_Payloads()
    {
        using var configured = CreateConfiguredFactory(factory);
        using var client = configured.CreateClient();
        var envelope = CreateEnvelope($"OUT-{Guid.NewGuid():N}", $"event-{Guid.NewGuid():N}", 1);

        var unknown = envelope with
        {
            EventType = "outbound.unknown",
            ExternalEventId = $"event-{Guid.NewGuid():N}",
        };
        (await SendAsync(client, unknown)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var incomplete = envelope with
        {
            ExternalEventId = $"event-{Guid.NewGuid():N}",
            Payload = JsonSerializer.SerializeToElement(new { outboundOrderId = envelope.ExternalObjectId }),
        };
        (await SendAsync(client, incomplete)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mapping_Api_Should_Upsert_Search_Validate_And_Deactivate_Connection_Values()
    {
        using var configured = CreateConfiguredFactory(factory);
        var token = await new AuthHelper(factory).GetRootAdminTokenAsync();
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string sku = $"SKU-{suffix}";
        string externalSku = $"EXT-{suffix}";

        using var createSku = await PutMappingAsync(client, new(
            WmsMappingKinds.Sku, sku, externalSku));
        createSku.StatusCode.ShouldBe(HttpStatusCode.OK, await createSku.Content.ReadAsStringAsync());
        var createdSku = await createSku.DeserializeAsync<WmsMappingDto>();
        createdSku.Provider.ShouldBe("reference-wms");
        createdSku.ConnectionId.ShouldBe("dev-primary");

        using var createUnit = await PutMappingAsync(client, new(
            WmsMappingKinds.Unit, "EA", $"CASE-{suffix}", 12));
        createUnit.StatusCode.ShouldBe(HttpStatusCode.OK, await createUnit.Content.ReadAsStringAsync());

        using var duplicateExternal = await PutMappingAsync(client, new(
            WmsMappingKinds.Sku, $"SKU-OTHER-{suffix}", externalSku));
        duplicateExternal.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var search = await client.GetAsync(
            $"/api/v1/wms/mappings?search={suffix}&pageNumber=1&pageSize=20");
        search.StatusCode.ShouldBe(HttpStatusCode.OK, await search.Content.ReadAsStringAsync());
        (await search.DeserializeAsync<PagedResponse<WmsMappingDto>>()).TotalCount.ShouldBe(2);

        using var validation = await client.PostAsJsonAsync("/api/v1/wms/mappings/validate", new
        {
            requirements = new[]
            {
                new { kind = WmsMappingKinds.Sku, foodOsValue = sku },
                new { kind = WmsMappingKinds.Unit, foodOsValue = "EA" },
                new { kind = WmsMappingKinds.Owner, foodOsValue = "root" },
            },
        });
        validation.StatusCode.ShouldBe(HttpStatusCode.OK, await validation.Content.ReadAsStringAsync());
        var validationResult = await validation.DeserializeAsync<WmsMappingValidationResult>();
        validationResult.IsValid.ShouldBeFalse();
        validationResult.Resolved.Count.ShouldBe(2);
        validationResult.Missing.ShouldHaveSingleItem().Reason.ShouldBe("missing");

        using var deactivate = await PutMappingAsync(client, new(
            WmsMappingKinds.Sku, sku, externalSku, IsActive: false));
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        (await deactivate.DeserializeAsync<WmsMappingDto>()).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Reservation_Gateway_Should_Query_The_Same_Operation_After_An_Unknown_Result()
    {
        var wms = new SequencedWmsClient();
        using var configured = CreateConfiguredFactory(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWmsStandardClient>();
                services.AddSingleton<IWmsStandardClient>(wms);
            }));
        var token = await new AuthHelper(factory).GetRootAdminTokenAsync();
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string warehouse = $"DC-{suffix}";
        string sku = $"SKU-{suffix}";
        foreach (var mapping in new[]
        {
            new UpsertWmsMappingCommand(WmsMappingKinds.Warehouse, warehouse, $"EXT-{warehouse}"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Owner, "root", $"OWNER-{suffix}"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Sku, sku, $"EXT-{sku}"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Unit, "EA", $"UNIT-{suffix}"),
        })
        {
            using var response = await PutMappingAsync(client, mapping);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        using var scope = configured.Services.CreateScope();
        var tenantSetter = scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>();
        string tenantId = $"restaurant-{suffix}";
        tenantSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(new AppTenantInfo(
            tenantId,
            tenantId,
            string.Empty,
            $"admin@{tenantId}.test",
            issuer: $"{tenantId}.issuer"));
        var gateway = scope.ServiceProvider.GetRequiredService<IWmsReservationGateway>();
        string key = Guid.NewGuid().ToString("N");
        var request = new WmsReserveOrderRequest(
            warehouse,
            "root",
            [new WmsReservationLine("line-1", sku, "EA", 2m)]);

        var unknown = await gateway.ReserveAsync(key, $"corr-{suffix}", request);
        unknown.Status.ShouldBe("unknown");
        var completed = await gateway.ReserveAsync(key, $"corr-{suffix}", request);
        completed.Status.ShouldBe("completed");
        completed.OperationId.ShouldBe(unknown.OperationId);
        completed.ReservationId.ShouldBe("WMS-RES-001");
        wms.Requests.Select(item => item.Kind).ShouldBe([WmsOperationKind.Reserve, WmsOperationKind.Query]);
        wms.Requests.Select(item => item.IdempotencyKey).Distinct().ShouldHaveSingleItem().ShouldBe(key);
    }

    [Fact]
    public async Task All_Standard_Event_Payload_Families_Should_Be_Accepted_With_Their_Entity_Type()
    {
        using var configured = CreateConfiguredFactory(factory);
        using var client = configured.CreateClient();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        object quantityLine = new { lineId = "line-1", sku = "SKU-001", uom = "EA", quantity = 1m };
        var cases = new (string EventType, string EntityType, object Payload)[]
        {
            (WmsEventTypes.InventorySnapshot, WmsEntityTypes.Inventory, new
            {
                warehouseId = "DC-01", ownerId = "root", sku = "SKU-001", uom = "EA",
                onHandQuantity = 10m, allocatedQuantity = 2m, availableQuantity = 8m, quarantinedQuantity = 0m,
            }),
            (WmsEventTypes.InventoryChanged, WmsEntityTypes.Inventory, new
            {
                warehouseId = "DC-01", ownerId = "root", sku = "SKU-001", uom = "EA",
                onHandQuantity = 9m, allocatedQuantity = 2m, availableQuantity = 7m, quarantinedQuantity = 0m,
            }),
            (WmsEventTypes.InventoryAdjusted, WmsEntityTypes.Inventory, new
            {
                warehouseId = "DC-01", ownerId = "root", sku = "SKU-001", uom = "EA",
                onHandQuantity = 8m, allocatedQuantity = 2m, availableQuantity = 6m, quarantinedQuantity = 0m,
            }),
            (WmsEventTypes.InboundReceived, WmsEntityTypes.InboundOrder, new
            {
                inboundOrderId = "IN-001", warehouseId = "DC-01", ownerId = "root", receivedAt = occurredAt,
                lines = new[] { quantityLine },
            }),
            (WmsEventTypes.InboundQualityCompleted, WmsEntityTypes.InboundOrder, new
            {
                inboundOrderId = "IN-001", warehouseId = "DC-01", ownerId = "root", completedAt = occurredAt,
                lines = new[] { new { lineId = "line-1", sku = "SKU-001", uom = "EA", acceptedQuantity = 1m, rejectedQuantity = 0m } },
            }),
            (WmsEventTypes.InboundPutawayCompleted, WmsEntityTypes.InboundOrder, new
            {
                inboundOrderId = "IN-001", warehouseId = "DC-01", ownerId = "root", completedAt = occurredAt,
                lines = new[] { new { lineId = "line-1", sku = "SKU-001", uom = "EA", quantity = 1m, locationCode = "A-01" } },
            }),
            (WmsEventTypes.OutboundAllocated, WmsEntityTypes.OutboundOrder, OutboundPayload("OUT-001", occurredAt, quantityLine)),
            (WmsEventTypes.OutboundPicked, WmsEntityTypes.OutboundOrder, OutboundPayload("OUT-002", occurredAt, quantityLine)),
            (WmsEventTypes.OutboundLoaded, WmsEntityTypes.OutboundOrder, OutboundPayload("OUT-003", occurredAt, quantityLine)),
            (WmsEventTypes.OutboundShipped, WmsEntityTypes.OutboundOrder, OutboundPayload("OUT-004", occurredAt, quantityLine)),
            (WmsEventTypes.OutboundShortage, WmsEntityTypes.OutboundOrder, new
            {
                outboundOrderId = "OUT-005", warehouseId = "DC-01", ownerId = "root", occurredAt,
                reasonCode = "insufficient_stock", lines = new[] { quantityLine },
            }),
            (WmsEventTypes.ReturnReceived, WmsEntityTypes.Return, new
            {
                returnId = "RET-001", outboundOrderId = "OUT-001", warehouseId = "DC-01", ownerId = "root", receivedAt = occurredAt,
                lines = new[] { new { lineId = "line-1", sku = "SKU-001", uom = "EA", quantity = 1m, disposition = "quarantine" } },
            }),
        };

        foreach (var testCase in cases)
        {
            string objectId = $"OBJ-{Guid.NewGuid():N}";
            var envelope = CreateEnvelope(objectId, $"event-{Guid.NewGuid():N}", 1) with
            {
                EventType = testCase.EventType,
                EntityType = testCase.EntityType,
                Payload = JsonSerializer.SerializeToElement(testCase.Payload),
            };
            using var response = await SendAsync(client, envelope);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{testCase.EventType}: {await response.Content.ReadAsStringAsync()}");
        }

        var wrongEntity = CreateEnvelope($"OBJ-{Guid.NewGuid():N}", $"event-{Guid.NewGuid():N}", 1) with
        {
            EventType = WmsEventTypes.InventoryChanged,
            EntityType = WmsEntityTypes.OutboundOrder,
            Payload = JsonSerializer.SerializeToElement(cases[0].Payload),
        };
        (await SendAsync(client, wrongEntity)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
        JsonSerializer.SerializeToElement(new
        {
            outboundOrderId = objectId,
            warehouseId = "DC-01",
            ownerId = "root",
            occurredAt = DateTimeOffset.UtcNow,
            lines = new[]
            {
                new { lineId = "line-1", sku = "SKU-001", uom = "EA", quantity = 1m },
            },
        }));

    private static WmsEventEnvelope CreateInventoryEnvelope(
        string objectId,
        string eventId,
        long sequence,
        string sku,
        decimal availableQuantity,
        DateTimeOffset? occurredAt = null) => new(
        Guid.CreateVersion7(),
        "reference-wms",
        "dev-primary",
        WmsEventTypes.InventoryChanged,
        WmsEntityTypes.Inventory,
        "1.0",
        eventId,
        objectId,
        sequence,
        occurredAt ?? DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        $"corr-{objectId}",
        null,
        $"reference-wms:dev-primary:{eventId}",
        JsonSerializer.SerializeToElement(new
        {
            warehouseId = "DC-01",
            ownerId = "root",
            sku,
            uom = "EA",
            onHandQuantity = availableQuantity,
            allocatedQuantity = 0m,
            availableQuantity,
            quarantinedQuantity = 0m,
        }));

    private static async Task AssertAvailabilityAsync(
        WebApplicationFactory<Program> factory,
        string sku,
        decimal expectedQuantity,
        decimal requiredQuantity,
        bool expectedAvailable)
    {
        using var scope = factory.Services.CreateScope();
        var tenantSetter = scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>();
        tenantSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(new AppTenantInfo(
            "restaurant-probe",
            "Restaurant Probe",
            string.Empty,
            "admin@restaurant-probe.test",
            issuer: "restaurant-probe.issuer"));
        var reader = scope.ServiceProvider.GetRequiredService<IWmsAvailabilityReader>();
        var result = await reader.GetAvailabilityAsync(
            "DC-01",
            [new WmsAvailabilityRequest(sku, "EA", requiredQuantity)],
            CancellationToken.None);
        result.Count.ShouldBe(1);
        var availability = result[0];
        availability.AvailableQuantity.ShouldBe(expectedQuantity);
        availability.IsAvailable.ShouldBe(expectedAvailable);
    }

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

    private static Task<HttpResponseMessage> PutMappingAsync(
        HttpClient client,
        UpsertWmsMappingCommand command)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/wms/mappings")
        {
            Content = JsonContent.Create(command),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static object OutboundPayload(string outboundOrderId, DateTimeOffset occurredAt, object line) => new
    {
        outboundOrderId,
        warehouseId = "DC-01",
        ownerId = "root",
        occurredAt,
        lines = new[] { line },
    };

    private static async Task<WmsEventReceipt> ReadReceiptAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<WmsEventReceipt>(await response.Content.ReadAsByteArrayAsync(), JsonOptions)
        ?? throw new InvalidOperationException("Missing WMS event receipt.");

    private sealed class SequencedWmsClient : IWmsStandardClient
    {
        public List<WmsOperationRequest> Requests { get; } = [];

        public Task<WmsOperationResponse> ExecuteAsync(
            WmsOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(request.Kind == WmsOperationKind.Reserve
                ? new WmsOperationResponse("unknown", null, "timeout", "Timed out after submission.", null)
                : new WmsOperationResponse("completed", "WMS-RES-001", null, null, null));
        }
    }
}
