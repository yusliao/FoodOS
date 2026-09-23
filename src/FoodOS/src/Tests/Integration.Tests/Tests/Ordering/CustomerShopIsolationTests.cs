using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using System.Text.Json;

namespace Integration.Tests.Tests.Ordering;

[Collection(FshCollectionDefinition.Name)]
public sealed partial class CustomerShopIsolationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public CustomerShopIsolationTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task RestaurantCustomers_Should_ShareCatalogAndKeepCartsIsolated_WhenPlacementIsWmsBlocked()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantA = $"shop-a-{suffix}";
        string tenantB = $"shop-b-{suffix}";
        string emailA = $"admin-{tenantA}@tenant.test";
        string emailB = $"admin-{tenantB}@tenant.test";
        await CreateTenantAsync(rootClient, tenantA, emailA);
        await CreateTenantAsync(rootClient, tenantB, emailB);
        await WaitForProvisioningAsync(rootClient, tenantA);
        await WaitForProvisioningAsync(rootClient, tenantB);

        var warehouse = await CreateWarehouseAsync(rootClient);
        Guid productId = await CreateProductAsync(rootClient);
        Guid orgA = await CreateCustomerOrgAsync(rootClient, tenantA, $"A{suffix}");
        Guid orgB = await CreateCustomerOrgAsync(rootClient, tenantB, $"B{suffix}");
        Guid storeA = await CreateStoreAsync(rootClient, orgA, warehouse.Id, $"SA{suffix}");
        Guid storeB = await CreateStoreAsync(rootClient, orgB, warehouse.Id, $"SB{suffix}");

        using var clientA = await CreateDashboardClientAsync(emailA, tenantA);
        using var clientB = await CreateDashboardClientAsync(emailB, tenantB);
        await GrantSelfStoreAccessAsync(clientA, storeA);
        await GrantSelfStoreAccessAsync(clientB, storeB);

        using var storesAResponse = await clientA.GetAsync($"{TestConstants.ShopBasePath}/stores");
        storesAResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await storesAResponse.Content.ReadAsStringAsync());
        (await storesAResponse.DeserializeAsync<IReadOnlyList<ShopStoreDto>>()).ShouldHaveSingleItem().Id.ShouldBe(storeA);
        using var storesBResponse = await clientB.GetAsync($"{TestConstants.ShopBasePath}/stores");
        storesBResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await storesBResponse.Content.ReadAsStringAsync());
        (await storesBResponse.DeserializeAsync<IReadOnlyList<ShopStoreDto>>()).ShouldHaveSingleItem().Id.ShouldBe(storeB);

        using var productsResponse = await clientA.GetAsync($"{TestConstants.ShopBasePath}/products?pageNumber=1&pageSize=20");
        productsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await productsResponse.Content.ReadAsStringAsync());
        var product = (await productsResponse.DeserializeAsync<PagedResult<ShopProductDto>>()).Items
            .Single(item => item.Id == productId);
        product.UnitPrice.ShouldBe(9.5m);
        product.IsAvailable.ShouldBeTrue();

        foreach (string path in new[]
        {
            $"{TestConstants.CatalogBasePath}/products?pageNumber=1&pageSize=20",
            $"{TestConstants.OrderingBasePath}/stores",
            $"{TestConstants.ProcurementBasePath}/suppliers",
            $"{TestConstants.WarehouseBasePath}/waves",
            $"{TestConstants.LogisticsBasePath}/shipments",
            TestConstants.AuditsBasePath,
        })
        {
            using var response = await clientA.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        using var updateCartA = await clientA.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeA}/cart",
            new { lines = new[] { new { productId, quantity = 3m } } });
        updateCartA.StatusCode.ShouldBe(HttpStatusCode.OK, await updateCartA.Content.ReadAsStringAsync());
        using var updateCartB = await clientB.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeB}/cart",
            new { lines = new[] { new { productId, quantity = 4m } } });
        updateCartB.StatusCode.ShouldBe(HttpStatusCode.OK, await updateCartB.Content.ReadAsStringAsync());

        foreach (var (client, storeId) in new[] { (clientA, storeA), (clientB, storeB) })
        {
            using var place = await client.PostAsJsonAsync(
                $"{TestConstants.ShopBasePath}/orders", new { storeId });
            place.StatusCode.ShouldBe(HttpStatusCode.Conflict, await place.Content.ReadAsStringAsync());
            (await place.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
        }

        await AssertCartPreservedAsync(clientA, storeA, productId, 3m);
        await AssertCartPreservedAsync(clientB, storeB, productId, 4m);
        await AssertForeignStoreHiddenAsync(clientA, storeB, productId);
        await AssertForeignStoreHiddenAsync(clientB, storeA, productId);

        foreach (var client in new[] { clientA, clientB })
        {
            using var orders = await client.GetAsync($"{TestConstants.ShopBasePath}/orders?pageNumber=1&pageSize=20");
            orders.StatusCode.ShouldBe(HttpStatusCode.OK, await orders.Content.ReadAsStringAsync());
            (await orders.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldBeEmpty();
            using var deliveries = await client.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
            deliveries.StatusCode.ShouldBe(HttpStatusCode.OK, await deliveries.Content.ReadAsStringAsync());
            (await deliveries.DeserializeAsync<IReadOnlyList<ShopDeliveryDto>>()).ShouldBeEmpty();
        }

        using var operatorOrders = await rootClient.GetAsync(
            $"{TestConstants.OrderingBasePath}/orders?pageNumber=1&pageSize=200");
        operatorOrders.StatusCode.ShouldBe(HttpStatusCode.OK, await operatorOrders.Content.ReadAsStringAsync());
        var allOrders = (await operatorOrders.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items;
        allOrders.ShouldNotContain(order => order.StoreId == storeA || order.StoreId == storeB);
    }

    private static async Task AssertForeignStoreHiddenAsync(HttpClient client, Guid storeId, Guid productId)
    {
        using var store = await client.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeId}");
        store.StatusCode.ShouldBe(HttpStatusCode.NotFound, await store.Content.ReadAsStringAsync());
        using var cart = await client.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeId}/cart");
        cart.StatusCode.ShouldBe(HttpStatusCode.NotFound, await cart.Content.ReadAsStringAsync());
        using var update = await client.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeId}/cart",
            new { lines = new[] { new { productId, quantity = 1m } } });
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound, await update.Content.ReadAsStringAsync());
        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.ShopBasePath}/orders", new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.Conflict, await place.Content.ReadAsStringAsync());
        (await place.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private async Task<HttpClient> CreateDashboardClientAsync(string email, string tenantId)
    {
        using var anonymous = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", tenantId);
        request.Headers.Add("X-FSH-App", "dashboard");
        request.Content = JsonContent.Create(new { email, password = TestConstants.DefaultPassword });
        using var response = await anonymous.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var token = JsonSerializer.Deserialize<TokenResult>(
            await response.Content.ReadAsStringAsync(), JsonOptions).ShouldNotBeNull();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", tenantId);
        return client;
    }

    private static async Task GrantSelfStoreAccessAsync(HttpClient client, Guid storeId)
    {
        using var profileResponse = await client.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await profileResponse.Content.ReadAsStringAsync());
        var profile = await profileResponse.DeserializeAsync<UserDto>();
        using var response = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/store-access/users/{profile.Id}",
            new { userId = profile.Id, storeIds = new[] { storeId } });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail)
    {
        using var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Restaurant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task WaitForProvisioningAsync(HttpClient client, string tenantId, int maxRetries = 60)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            using var response = await client.GetAsync($"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
            string content = await response.Content.ReadAsStringAsync();
            var status = response.IsSuccessStatusCode
                ? (await response.DeserializeAsync<TenantProvisioningStatusDto>()).Status
                : null;
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tenant {tenantId} provisioning failed: {content}");
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Tenant {tenantId} did not finish provisioning.");
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        string code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        Guid id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client)
    {
        using var brand = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/brands",
            new { name = $"Brand-{Guid.NewGuid():N}", description = (string?)null, logoUrl = (string?)null });
        brand.EnsureSuccessStatusCode();
        using var category = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/categories",
            new { name = $"Category-{Guid.NewGuid():N}", description = (string?)null, parentCategoryId = (Guid?)null });
        category.EnsureSuccessStatusCode();
        using var product = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/products", new
        {
            sku = $"SHOP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            name = $"Food-{Guid.NewGuid():N}",
            description = "Restaurant product",
            brandId = await brand.DeserializeAsync<Guid>(),
            categoryId = await category.DeserializeAsync<Guid>(),
            priceAmount = 9.5m,
            priceCurrency = "USD",
            stock = 0,
        });
        product.StatusCode.ShouldBe(HttpStatusCode.OK, await product.Content.ReadAsStringAsync());
        return await product.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateCustomerOrgAsync(HttpClient client, string tenantId, string code)
    {
        using var response = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/customer-orgs",
            new { code, name = $"Restaurant {code}", customerTenantId = tenantId });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateStoreAsync(HttpClient client, Guid orgId, Guid warehouseId, string code)
    {
        using var response = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/stores", new
        {
            customerOrgId = orgId,
            code,
            name = $"Store {code}",
            address = "1 Harbor St",
            defaultWarehouseId = warehouseId,
            defaultRouteId = (Guid?)null,
            deliveryWindow = "05:00-08:00",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

}
