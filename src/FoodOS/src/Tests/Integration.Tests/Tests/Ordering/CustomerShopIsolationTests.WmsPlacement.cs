using System.Net.Http.Headers;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
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
    public async Task Shop_Placement_Should_Preserve_The_Cart_Until_Wms_Reservation_Is_Confirmed()
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
        capabilities.AcceptsOrderChanges.ShouldBeFalse();
        capabilities.BlockingReasons.ShouldBeEmpty();
        using var updateCart = await customer.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeId}/cart",
            new { lines = new[] { new { productId, quantity = 3m } } });
        updateCart.StatusCode.ShouldBe(HttpStatusCode.OK, await updateCart.Content.ReadAsStringAsync());

        string idempotencyKey = Guid.NewGuid().ToString("N");
        using var pending = await PostShopOrderAsync(customer, storeId, idempotencyKey);
        pending.StatusCode.ShouldBe(HttpStatusCode.Conflict, await pending.Content.ReadAsStringAsync());
        await AssertCartPreservedAsync(customer, storeId, productId, 3m);

        using var completed = await PostShopOrderAsync(customer, storeId, idempotencyKey);
        completed.StatusCode.ShouldBe(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        Guid orderId = await completed.DeserializeAsync<Guid>();
        using var cartResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeId}/cart");
        (await cartResponse.DeserializeAsync<ShopCartDto>()).Lines.ShouldBeEmpty();
        using var orderResponse = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        var order = await orderResponse.DeserializeAsync<ShopOrderDto>();
        order.Status.ShouldBe("Reserved");
        order.Lines.ShouldHaveSingleItem().OrderedQty.ShouldBe(3m);
        wms.Requests.Select(item => item.Kind).ShouldBe([WmsOperationKind.Reserve, WmsOperationKind.Query]);
        wms.Requests.Select(item => item.IdempotencyKey).Distinct().ShouldHaveSingleItem().ShouldBe(idempotencyKey);
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

    private sealed class SequencedReservationClient : IWmsStandardClient
    {
        public List<WmsOperationRequest> Requests { get; } = [];

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<WmsOperationResponse> ExecuteAsync(
            WmsOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(request.Kind == WmsOperationKind.Reserve
                ? new WmsOperationResponse("unknown", null, "timeout", "Timed out after submission.", null)
                : new WmsOperationResponse("completed", "WMS-RES-SHOP-001", null, null, null));
        }
    }
}
