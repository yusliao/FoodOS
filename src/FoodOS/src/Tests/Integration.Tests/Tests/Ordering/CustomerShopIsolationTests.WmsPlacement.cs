using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Jobs;
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

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    [Fact]
    public async Task Shop_Placement_Should_Commit_Locally_And_Leave_Warehouse_Notification_Pending()
    {
        var wms = new SequencedReservationClient();
        var configured = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["WmsIntegration:Enabled"] = "true",
                    ["WmsIntegration:Provider"] = "reference-wms",
                    ["WmsIntegration:ConnectionId"] = "shop-placement",
                    ["WmsIntegration:WarehouseId"] = "configured-by-mapping",
                    ["WmsIntegration:Tenant"] = "root",
                    ["WmsIntegration:BaseUrl"] = "https://wms.invalid/",
                    ["WmsIntegration:SigningSecret"] = "shop-placement-integration-secret",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWmsStandardClient>();
                services.AddSingleton<IWmsStandardClient>(wms);
            });
        });

        var rootToken = await _auth.GetRootAdminTokenAsync();
        using var rootClient = configured.CreateClient();
        rootClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rootToken.AccessToken);
        rootClient.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantId = $"shop-wms-{suffix}";
        string email = $"admin-{tenantId}@tenant.test";
        await CreateTenantAsync(rootClient, tenantId, email);
        await WaitForProvisioningAsync(rootClient, tenantId);
        var warehouse = await CreateWarehouseAsync(rootClient);
        Guid productId = await CreateProductAsync(rootClient);
        using var productResponse = await rootClient.GetAsync($"{TestConstants.CatalogBasePath}/products/{productId}");
        productResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await productResponse.Content.ReadAsStringAsync());
        var product = await productResponse.DeserializeAsync<ProductDto>();
        Guid orgId = await CreateCustomerOrgAsync(rootClient, tenantId, $"O{suffix}");
        Guid storeId = await CreateStoreAsync(rootClient, orgId, warehouse.Id, $"S{suffix}");

        foreach (var mapping in new[]
        {
            new UpsertWmsMappingCommand(WmsMappingKinds.Warehouse, "configured-by-mapping", "EXT-CONFIGURED"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Warehouse, warehouse.Code, $"EXT-{warehouse.Code}"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Owner, "root", "EXT-ROOT"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Sku, product.Sku, $"EXT-{product.Sku}"),
            new UpsertWmsMappingCommand(WmsMappingKinds.Unit, product.BaseUom, "EXT-EA"),
        })
        {
            using var mappingResponse = await PutShopMappingAsync(rootClient, mapping);
            mappingResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await mappingResponse.Content.ReadAsStringAsync());
        }

        using var customer = await CreateDashboardClientAsync(email, tenantId, configured);
        await GrantSelfStoreAccessAsync(customer, storeId);
        using var capabilitiesResponse = await customer.GetAsync("/api/v1/fulfillment/capabilities");
        capabilitiesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await capabilitiesResponse.Content.ReadAsStringAsync());
        var capabilities = await capabilitiesResponse.DeserializeAsync<WmsReadinessSnapshot>();
        capabilities.Readiness.ShouldBe("ready");
        capabilities.AcceptsOrders.ShouldBeTrue();
        capabilities.AcceptsOrderChanges.ShouldBeTrue();
        capabilities.BlockingReasons.ShouldBeEmpty();
        using var updateCart = await customer.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeId}/cart",
            new { lines = new[] { new { productId, quantity = 3m } } });
        updateCart.StatusCode.ShouldBe(HttpStatusCode.OK, await updateCart.Content.ReadAsStringAsync());

        string idempotencyKey = Guid.NewGuid().ToString("N");
        using var placed = await PostShopOrderAsync(customer, storeId, idempotencyKey);
        placed.StatusCode.ShouldBe(HttpStatusCode.OK, await placed.Content.ReadAsStringAsync());
        Guid orderId = await placed.DeserializeAsync<Guid>();
        using var repeated = await PostShopOrderAsync(customer, storeId, idempotencyKey);
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK, await repeated.Content.ReadAsStringAsync());
        (await repeated.DeserializeAsync<Guid>()).ShouldBe(orderId);
        using var cartResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeId}/cart");
        (await cartResponse.DeserializeAsync<ShopCartDto>()).Lines.ShouldBeEmpty();
        using var orderResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        var order = await orderResponse.DeserializeAsync<ShopOrderDto>();
        order.Status.ShouldBe("Reserved");
        order.WarehouseConfirmationStatus.ShouldBe("Pending");
        order.Lines.ShouldHaveSingleItem().OrderedQty.ShouldBe(3m);
        wms.Requests.ShouldBeEmpty();

        using (var notificationScope = configured.Services.CreateScope())
        {
            var tenantStore = notificationScope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
            var rootTenant = await tenantStore.GetAsync(TestConstants.RootTenantId);
            rootTenant.ShouldNotBeNull();
            var notificationJob = notificationScope.ServiceProvider.GetRequiredService<WmsOrderNotificationJob>();
            (await notificationJob.ProcessTenantAsync(
                rootTenant,
                DateTimeOffset.UtcNow.AddMinutes(1),
                CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);
        }

        wms.Requests.ShouldHaveSingleItem().Kind.ShouldBe(WmsOperationKind.SubmitOutboundOrder);
        using var confirmedResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        (await confirmedResponse.DeserializeAsync<ShopOrderDto>()).WarehouseConfirmationStatus.ShouldBe("Confirmed");

        var orderLine = order.Lines.ShouldHaveSingleItem();
        var feedback = new WmsEventEnvelope(
            Guid.CreateVersion7(),
            "reference-wms",
            "shop-placement",
            WmsEventTypes.OutboundShortage,
            WmsEntityTypes.OutboundOrder,
            "1.0",
            $"event-{Guid.NewGuid():N}",
            orderId.ToString(),
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            $"corr-{orderId:N}",
            null,
            $"shortage:{orderId:N}:1",
            JsonSerializer.SerializeToElement(new
            {
                outboundOrderId = orderId,
                warehouseId = warehouse.Code,
                ownerId = "root",
                occurredAt = DateTimeOffset.UtcNow,
                reasonCode = "short_stock",
                lines = new[]
                {
                    new { lineId = orderLine.Id, sku = product.Sku, uom = product.BaseUom, quantity = 1m },
                },
            }));
        byte[] feedbackBody = JsonSerializer.SerializeToUtf8Bytes(
            feedback,
            JsonOptions);
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wms/inbound/events")
        {
            Content = new ByteArrayContent(feedbackBody),
        };
        feedbackRequest.Content.Headers.ContentType = new("application/json");
        feedbackRequest.Headers.Add("X-FoodOS-WMS-Timestamp", timestamp);
        feedbackRequest.Headers.Add(
            "X-FoodOS-WMS-Signature",
            WmsSignature.Sign(timestamp, feedbackBody, "shop-placement-integration-secret"));
        using var feedbackResponse = await rootClient.SendAsync(feedbackRequest);
        feedbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await feedbackResponse.Content.ReadAsStringAsync());

        using var exceptionResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        var exceptionOrder = await exceptionResponse.DeserializeAsync<ShopOrderDto>();
        exceptionOrder.WarehouseConfirmationStatus.ShouldBe("Exception");
        exceptionOrder.Lines.ShouldHaveSingleItem().ShortageQty.ShouldBe(1m);

        using var amend = await PostShopAmendAsync(customer, orderId, productId, 4m);
        amend.StatusCode.ShouldBe(HttpStatusCode.OK, await amend.Content.ReadAsStringAsync());
        wms.Requests.Count.ShouldBe(1);
        using var amendedResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        var amendedOrder = await amendedResponse.DeserializeAsync<ShopOrderDto>();
        amendedOrder.Revision.ShouldBe(1);
        amendedOrder.WarehouseConfirmationStatus.ShouldBe("Pending");
        amendedOrder.Lines.ShouldHaveSingleItem().OrderedQty.ShouldBe(4m);

        await ProcessNotificationsAsync(configured, DateTimeOffset.UtcNow.AddMinutes(2));
        wms.Requests.Count.ShouldBe(2);
        wms.Requests[1].Kind.ShouldBe(WmsOperationKind.SubmitOutboundOrder);

        using var cancel = await PostShopCancelAsync(customer, orderId);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        using var cancelledResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        var cancelledOrder = await cancelledResponse.DeserializeAsync<ShopOrderDto>();
        cancelledOrder.Status.ShouldBe("Cancelled");
        cancelledOrder.WarehouseConfirmationStatus.ShouldBe("Pending");

        await ProcessNotificationsAsync(configured, DateTimeOffset.UtcNow.AddMinutes(3));
        wms.Requests.Count.ShouldBe(3);
        wms.Requests[2].Kind.ShouldBe(WmsOperationKind.CancelOutboundOrder);
        using var cancellationPending = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        (await cancellationPending.DeserializeAsync<ShopOrderDto>())
            .WarehouseConfirmationStatus.ShouldBe("Pending");

        await ProcessNotificationsAsync(configured, DateTimeOffset.UtcNow.AddMinutes(4));
        wms.Requests.Count.ShouldBe(4);
        wms.Requests[3].Kind.ShouldBe(WmsOperationKind.CancelOutboundOrder);
        wms.Requests[3].IdempotencyKey.ShouldBe(wms.Requests[2].IdempotencyKey);
        wms.Requests[3].Payload.GetProperty("operationId").GetGuid().ShouldBe(orderId);
        wms.Requests[3].Payload.GetProperty("outboundOrderId").GetGuid().ShouldBe(orderId);
        wms.Requests[3].Payload.GetProperty("reason").GetString()
            .ShouldBe(wms.Requests[2].Payload.GetProperty("reason").GetString());
        using var cancellationConfirmed = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        (await cancellationConfirmed.DeserializeAsync<ShopOrderDto>())
            .WarehouseConfirmationStatus.ShouldBe("Confirmed");

        using var verificationScope = configured.Services.CreateScope();
        var verificationTenantStore = verificationScope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var verificationRootTenant = await verificationTenantStore.GetAsync(TestConstants.RootTenantId);
        verificationScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(verificationRootTenant);
        var inventory = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await inventory.Reservations.AnyAsync(reservation => reservation.OrderId == orderId)).ShouldBeFalse();
    }

    private static Task<HttpResponseMessage> PostShopOrderAsync(
        HttpClient client,
        Guid storeId,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.ShopBasePath}/orders")
        {
            Content = JsonContent.Create(new { storeId }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PutShopMappingAsync(
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

    private static Task<HttpResponseMessage> PostShopAmendAsync(
        HttpClient client,
        Guid orderId,
        Guid productId,
        decimal quantity)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{TestConstants.ShopBasePath}/orders/{orderId}/amend")
        {
            Content = JsonContent.Create(new { lines = new[] { new { productId, quantity } } }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostShopCancelAsync(HttpClient client, Guid orderId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{TestConstants.ShopBasePath}/orders/{orderId}/cancel");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static async Task ProcessNotificationsAsync(
        WebApplicationFactory<Program> application,
        DateTimeOffset utcNow)
    {
        using var scope = application.Services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var rootTenant = await tenantStore.GetAsync(TestConstants.RootTenantId);
        rootTenant.ShouldNotBeNull();
        var job = scope.ServiceProvider.GetRequiredService<WmsOrderNotificationJob>();
        (await job.ProcessTenantAsync(rootTenant, utcNow, CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);
    }

    private sealed class SequencedReservationClient : IWmsStandardClient
    {
        public List<WmsOperationRequest> Requests { get; } = [];

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<WmsOperationResponse> ExecuteAsync(
            WmsOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Kind == WmsOperationKind.CancelOutboundOrder
                && Requests.Count(item => item.Kind == WmsOperationKind.CancelOutboundOrder) == 1)
            {
                return Task.FromResult(
                    new WmsOperationResponse("unknown", null, "timeout", "Timed out after submission.", null));
            }
            return Task.FromResult(request.Kind == WmsOperationKind.Reserve
                ? new WmsOperationResponse("unknown", null, "timeout", "Timed out after submission.", null)
                : new WmsOperationResponse("completed", "WMS-RES-SHOP-001", null, null, null));
        }
    }
}
