using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using System.Text.Json;

namespace Integration.Tests.Tests.Ordering;

[Collection(FshCollectionDefinition.Name)]
public sealed class CustomerShopIsolationTests
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
    public async Task RestaurantCustomers_Should_ShareOperatorCatalog_But_Not_EachOthersBusinessData()
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
        await ReceiveAsync(rootClient, warehouse.Id, productId, $"LOT-{suffix}", 20m);
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
        var storesA = await storesAResponse.DeserializeAsync<IReadOnlyList<ShopStoreDto>>();
        storesA.ShouldHaveSingleItem().Id.ShouldBe(storeA);

        using var foreignStore = await clientB.GetAsync($"{TestConstants.ShopBasePath}/stores/{storeA}");
        foreignStore.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignStore.Content.ReadAsStringAsync());

        using var productsResponse = await clientA.GetAsync($"{TestConstants.ShopBasePath}/products?pageNumber=1&pageSize=20");
        productsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await productsResponse.Content.ReadAsStringAsync());
        var products = await productsResponse.DeserializeAsync<PagedResult<ShopProductDto>>();
        var product = products.Items.Single(item => item.Id == productId);
        product.UnitPrice.ShouldBe(9.5m);
        product.IsAvailable.ShouldBeTrue();

        using var oldCatalogResponse = await clientA.GetAsync($"{TestConstants.CatalogBasePath}/products?pageNumber=1&pageSize=20");
        oldCatalogResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var operatorStoresResponse = await clientA.GetAsync($"{TestConstants.OrderingBasePath}/stores");
        operatorStoresResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var suppliersResponse = await clientA.GetAsync($"{TestConstants.ProcurementBasePath}/suppliers");
        suppliersResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var warehouseResponse = await clientA.GetAsync($"{TestConstants.WarehouseBasePath}/waves");
        warehouseResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var logisticsResponse = await clientA.GetAsync($"{TestConstants.LogisticsBasePath}/shipments");
        logisticsResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var auditResponse = await clientA.GetAsync($"{TestConstants.AuditsBasePath}");
        auditResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var deliveriesResponse = await clientA.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
        deliveriesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deliveriesResponse.Content.ReadAsStringAsync());
        (await deliveriesResponse.DeserializeAsync<IReadOnlyList<ShopDeliveryDto>>()).ShouldBeEmpty();

        using var updateCart = await clientA.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeA}/cart",
            new { lines = new[] { new { productId, quantity = 3m } } });
        updateCart.StatusCode.ShouldBe(HttpStatusCode.OK, await updateCart.Content.ReadAsStringAsync());

        using var place = await clientA.PostAsJsonAsync(
            $"{TestConstants.ShopBasePath}/orders",
            new { storeId = storeA });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        Guid orderId = await place.DeserializeAsync<Guid>();

        using var ownOrder = await clientA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        ownOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await ownOrder.Content.ReadAsStringAsync());
        var order = await ownOrder.DeserializeAsync<ShopOrderDto>();
        order.StoreId.ShouldBe(storeA);
        AssertOrderPayload(await ownOrder.Content.ReadAsStringAsync());

        using var operatorReconcile = await clientA.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/reconcile",
            new { });
        operatorReconcile.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var foreignOrder = await clientB.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        foreignOrder.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignOrder.Content.ReadAsStringAsync());

        using var cartB = await clientB.PutAsJsonAsync(
            $"{TestConstants.ShopBasePath}/stores/{storeB}/cart",
            new { lines = new[] { new { productId, quantity = 4m } } });
        cartB.StatusCode.ShouldBe(HttpStatusCode.OK, await cartB.Content.ReadAsStringAsync());
        using var placeB = await clientB.PostAsJsonAsync(
            $"{TestConstants.ShopBasePath}/orders", new { storeId = storeB });
        placeB.StatusCode.ShouldBe(HttpStatusCode.OK, await placeB.Content.ReadAsStringAsync());
        Guid otherOrderId = await placeB.DeserializeAsync<Guid>();
        otherOrderId.ShouldNotBe(orderId);
        using var ownOrderB = await clientB.GetAsync($"{TestConstants.ShopBasePath}/orders/{otherOrderId}");
        ownOrderB.StatusCode.ShouldBe(HttpStatusCode.OK, await ownOrderB.Content.ReadAsStringAsync());
        var orderB = await ownOrderB.DeserializeAsync<ShopOrderDto>();
        orderB.StoreId.ShouldBe(storeB);
        orderB.Lines.Single().UnitPrice.ShouldBe(9.5m);
        orderB.Lines.Single().OrderedQty.ShouldBe(4m);
        AssertOrderPayload(await ownOrderB.Content.ReadAsStringAsync());

        await AssertCannotAccessOtherCustomerAsync(clientA, storeB, orderB, productId);
        await AssertCannotAccessOtherCustomerAsync(clientB, storeA, order, productId);
        await AssertOwnOrderListAsync(clientA, orderId, otherOrderId);
        await AssertOwnOrderListAsync(clientB, otherOrderId, orderId);

        using var operatorOrders = await rootClient.GetAsync(
            $"{TestConstants.OrderingBasePath}/orders?pageNumber=1&pageSize=200");
        operatorOrders.StatusCode.ShouldBe(HttpStatusCode.OK, await operatorOrders.Content.ReadAsStringAsync());
        var allOrders = (await operatorOrders.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items;
        var operatorA = allOrders.Single(item => item.Id == orderId);
        var operatorB = allOrders.Single(item => item.Id == otherOrderId);
        operatorA.CustomerTenantId.ShouldBe(tenantA.ToUpperInvariant());
        operatorB.CustomerTenantId.ShouldBe(tenantB.ToUpperInvariant());
        operatorA.Lines.Single().OrderedQty.ShouldBe(3m);
        operatorB.Lines.Single().OrderedQty.ShouldBe(4m);
        operatorA.Status.ShouldBe(order.Status);
        operatorB.Status.ShouldBe(orderB.Status);

        var shipment = await FulfillMixedShipmentAsync(
            rootClient, clientA, clientB,
            warehouse.Id,
            storeA,
            orderId,
            storeB,
            otherOrderId);
        Guid shipmentId = shipment.Id;
        foreach (var (customer, id) in new[] { (clientA, orderId), (clientB, otherOrderId) })
        {
            using var progress = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{id}");
            (await progress.DeserializeAsync<ShopOrderDto>()).Status.ShouldBe("InTransit");
            AssertOrderPayload(await progress.Content.ReadAsStringAsync());
            using var kpis = await customer.GetAsync($"{TestConstants.OpsBasePath}/kpis");
            kpis.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var trace = await customer.GetAsync(
                $"{TestConstants.OpsBasePath}/lots/{shipment.Lines[0].Lots[0].LotId}/trace");
            trace.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        using var deliveriesAResponse = await clientA.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
        deliveriesAResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deliveriesAResponse.Content.ReadAsStringAsync());
        var deliveriesA = await deliveriesAResponse.DeserializeAsync<IReadOnlyList<ShopDeliveryDto>>();
        var deliveryA = deliveriesA.Single(item => item.ShipmentId == shipmentId);
        deliveryA.StoreId.ShouldBe(storeA);
        deliveryA.OrderIds.ShouldBe([orderId]);
        deliveryA.OrderIds.ShouldNotContain(otherOrderId);
        deliveryA.ShipmentStatus.ShouldBe("Departed");
        AssertDeliveryPayload(await deliveriesAResponse.Content.ReadAsStringAsync(), storeB, otherOrderId, shipment);

        using var deliveriesBResponse = await clientB.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
        deliveriesBResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deliveriesBResponse.Content.ReadAsStringAsync());
        var deliveriesB = await deliveriesBResponse.DeserializeAsync<IReadOnlyList<ShopDeliveryDto>>();
        var deliveryB = deliveriesB.Single(item => item.ShipmentId == shipmentId);
        deliveryB.StoreId.ShouldBe(storeB);
        deliveryB.OrderIds.ShouldBe([otherOrderId]);
        deliveryB.OrderIds.ShouldNotContain(orderId);
        AssertDeliveryPayload(await deliveriesBResponse.Content.ReadAsStringAsync(), storeA, orderId, shipment);

        using var foreignDeliveryFilter = await clientA.GetAsync(
            $"{TestConstants.ShopBasePath}/deliveries?storeId={storeB}");
        foreignDeliveryFilter.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var reverseDeliveryFilter = await clientB.GetAsync($"{TestConstants.ShopBasePath}/deliveries?storeId={storeA}");
        reverseDeliveryFilter.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        foreach (var customer in new[] { clientA, clientB })
        {
            using var internalDetail = await customer.GetAsync($"{TestConstants.LogisticsBasePath}/shipments/{shipmentId}");
            internalDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var driverDetail = await customer.GetAsync($"{TestConstants.LogisticsBasePath}/shipments/mine/{shipmentId}");
            driverDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var stopA = shipment.Stops.Single(s => s.StoreId == storeA);
        var lotA = shipment.Lines.Single(l => l.OrderId == orderId).Lots.ShouldHaveSingleItem();
        var signature = new
        {
            lines = new[] { new { orderLineId = lotA.OrderLineId, lotId = lotA.LotId, signedQty = lotA.Quantity } },
            signerName = "Restaurant A private signer", photoFileIds = Array.Empty<Guid>(), geo = "42.36,-71.06",
        };
        using var customerPod = await clientB.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/stops/{stopA.Id}/pod", signature);
        customerPod.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var signed = await rootClient.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/stops/{stopA.Id}/pod", signature);
        signed.StatusCode.ShouldBe(HttpStatusCode.OK, await signed.Content.ReadAsStringAsync());
        using var afterSignA = await clientA.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
        var signedA = (await afterSignA.DeserializeAsync<List<ShopDeliveryDto>>()).ShouldHaveSingleItem();
        signedA.SignedAt.ShouldNotBeNull();
        using var receivedOrder = await clientA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        (await receivedOrder.DeserializeAsync<ShopOrderDto>()).Status.ShouldBe("Received");
        AssertOrderPayload(await receivedOrder.Content.ReadAsStringAsync());
        using var afterSignB = await clientB.GetAsync($"{TestConstants.ShopBasePath}/deliveries");
        var unsignedB = (await afterSignB.DeserializeAsync<List<ShopDeliveryDto>>()).ShouldHaveSingleItem();
        unsignedB.SignedAt.ShouldBeNull();
        AssertDeliveryPayload(await afterSignB.Content.ReadAsStringAsync(), storeA, orderId, shipment);
        (await afterSignB.Content.ReadAsStringAsync()).ShouldNotContain("Restaurant A private signer");

        using var foreignAfterSales = await clientB.PostAsJsonAsync(
            $"{TestConstants.ShopBasePath}/after-sales",
            new
            {
                orderId,
                orderLineId = order.Lines.Single().Id,
                type = "Shortage",
                quantity = 1m,
                reason = "Must not reveal another restaurant's order",
            });
        foreignAfterSales.StatusCode.ShouldBe(
            HttpStatusCode.NotFound,
            await foreignAfterSales.Content.ReadAsStringAsync());

        using var foreignTickets = await clientB.GetAsync(
            $"{TestConstants.ShopBasePath}/after-sales?orderId={orderId}");
        foreignTickets.StatusCode.ShouldBe(HttpStatusCode.OK, await foreignTickets.Content.ReadAsStringAsync());
        (await foreignTickets.DeserializeAsync<IReadOnlyList<ShopAfterSalesTicketDto>>()).ShouldBeEmpty();

        using var operatorOrder = await rootClient.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        operatorOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await operatorOrder.Content.ReadAsStringAsync());
    }

    private static async Task AssertOwnOrderListAsync(HttpClient client, Guid ownId, Guid foreignId)
    {
        using var response = await client.GetAsync($"{TestConstants.ShopBasePath}/orders?pageNumber=1&pageSize=20");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var orders = (await response.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items;
        orders.ShouldHaveSingleItem().Id.ShouldBe(ownId);
        orders.ShouldNotContain(order => order.Id == foreignId);
    }

    private static async Task AssertCannotAccessOtherCustomerAsync(
        HttpClient client, Guid foreignStoreId, ShopOrderDto foreignOrder, Guid productId)
    {
        foreach (string path in new[]
        {
            $"/stores/{foreignStoreId}", $"/stores/{foreignStoreId}/cart",
            $"/orders/{foreignOrder.Id}", $"/orders?storeId={foreignStoreId}&pageNumber=1&pageSize=20",
        })
        {
            using var response = await client.GetAsync($"{TestConstants.ShopBasePath}{path}");
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }

        using var cart = await client.PutAsJsonAsync($"{TestConstants.ShopBasePath}/stores/{foreignStoreId}/cart",
            new { lines = new[] { new { productId, quantity = 1m } } });
        cart.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var place = await client.PostAsJsonAsync($"{TestConstants.ShopBasePath}/orders",
            new { storeId = foreignStoreId });
        place.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var amend = await client.PostAsJsonAsync($"{TestConstants.ShopBasePath}/orders/{foreignOrder.Id}/amend",
            new { lines = new[] { new { productId, quantity = 1m } } });
        amend.StatusCode.ShouldBe(HttpStatusCode.NotFound, await amend.Content.ReadAsStringAsync());
        using var cancel = await client.PostAsJsonAsync($"{TestConstants.ShopBasePath}/orders/{foreignOrder.Id}/cancel", new { });
        cancel.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var afterSales = await client.PostAsJsonAsync($"{TestConstants.ShopBasePath}/after-sales", new
        {
            orderId = foreignOrder.Id, orderLineId = foreignOrder.Lines.Single().Id,
            type = "Shortage", quantity = 1m, reason = "Cross-customer attempt",
        });
        afterSales.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    private static async Task ReceiveAsync(HttpClient client, Guid warehouseId, Guid productId, string lotNo, decimal quantity)
    {
        using var response = await client.PostAsJsonAsync($"{TestConstants.InventoryBasePath}/stock/receive", new
        {
            warehouseId,
            zone = "Ambient",
            productId,
            lotNo,
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            quantity,
            idempotencyKey = $"recv-{Guid.NewGuid():N}",
            manufacturedOn = (DateOnly?)null,
            origin = "Boston",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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

    private static void AssertOrderPayload(string json)
    {
        using var document = JsonDocument.Parse(json);
        var orderFields = new[] { "id", "number", "storeId", "status", "businessDate", "cutoffAt", "placedAt", "revision", "lines" };
        var lineFields = new[] { "id", "productId", "orderedQty", "deliveredQty", "returnedQty", "shortageQty", "shortageReason", "unitPrice", "currency" };
        foreach (var property in document.RootElement.EnumerateObject()) orderFields.ShouldContain(property.Name);
        foreach (var line in document.RootElement.GetProperty("lines").EnumerateArray())
            foreach (var property in line.EnumerateObject()) lineFields.ShouldContain(property.Name);
    }

    private static void AssertDeliveryPayload(string json, Guid foreignStoreId, Guid foreignOrderId, ShipmentDto shipment)
    {
        json.ShouldNotContain(foreignStoreId.ToString());
        json.ShouldNotContain(foreignOrderId.ToString());
        json.ShouldNotContain(shipment.DriverId.ToString());
        json.ShouldNotContain(shipment.VehicleId.ToString());
        json.ShouldNotContain(shipment.RouteId.ToString());
        using var document = JsonDocument.Parse(json);
        var allowed = new[] { "shipmentId", "shipmentNumber", "storeId", "businessDate", "shipmentStatus",
            "stopStatus", "sequence", "deliveryWindow", "signedAt", "orderIds" };
        foreach (var delivery in document.RootElement.EnumerateArray())
            foreach (var property in delivery.EnumerateObject()) allowed.ShouldContain(property.Name);
    }

    private static async Task<ShipmentDto> FulfillMixedShipmentAsync(
        HttpClient admin,
        HttpClient customerA,
        HttpClient customerB,
        Guid warehouseId,
        Guid storeA,
        Guid orderA,
        Guid storeB,
        Guid orderB)
    {
        using var cutoff = await admin.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/warehouses/{warehouseId}/cutoff", new { });
        cutoff.EnsureSuccessStatusCode();
        var plan = await cutoff.DeserializeAsync<CutoffResultDto>();
        plan.OrdersLocked.ShouldBe(2);
        using var generate = await admin.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId, businessDate = plan.BusinessDate });
        generate.EnsureSuccessStatusCode();
        var wave = (await generate.DeserializeAsync<List<WaveDto>>()).ShouldHaveSingleItem();
        string assignment = $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/assign";
        using var foreignAssignee = await admin.PostAsJsonAsync(assignment,
            new { pickerUserId = await WaveAssignments.UserIdAsync(customerA) });
        foreignAssignee.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var customer in new[] { customerA, customerB })
        {
            using var assign = await customer.PostAsJsonAsync(assignment,
                new { pickerUserId = await WaveAssignments.UserIdAsync(admin) });
            assign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var waveDetail = await customer.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}");
            waveDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        await WaveAssignments.AssignToSelfAsync(admin, wave.Id);
        using var release = await admin.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release", new { });
        release.EnsureSuccessStatusCode();
        var released = await release.DeserializeAsync<WaveDto>();
        released.Tasks.Count.ShouldBe(2);
        foreach (var task in released.Tasks)
        {
            using var customerPick = await customerA.PostAsJsonAsync(
                $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm", new { scannedLotId = task.LotId });
            customerPick.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            using var pick = await admin.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
                new { scannedLotId = task.LotId });
            pick.EnsureSuccessStatusCode();
        }
        using var pack = await admin.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/pack",
            new { orderIds = new[] { orderA, orderB }, sscc = $"SSCC{Guid.NewGuid():N}"[..18] });
        pack.EnsureSuccessStatusCode();
        var tote = await pack.DeserializeAsync<PackToteDto>();
        using var vehicle = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/vehicles",
            new { plate = $"M{Guid.NewGuid():N}"[..8], compartmentZones = "Ambient", payloadKg = 1000m });
        vehicle.EnsureSuccessStatusCode();
        Guid vehicleId = await vehicle.DeserializeAsync<Guid>();
        using var driver = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/drivers",
            new { userId = await WaveAssignments.UserIdAsync(admin), phone = "+16175550111" });
        driver.EnsureSuccessStatusCode();
        Guid driverId = await driver.DeserializeAsync<Guid>();
        using var route = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/routes",
            new { warehouseId, code = $"MIX{Guid.NewGuid():N}"[..12], storeIds = new[] { storeA, storeB }, defaultVehicleId = vehicleId });
        route.EnsureSuccessStatusCode();
        Guid routeId = await route.DeserializeAsync<Guid>();
        using var create = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/shipments",
            new { routeId, warehouseId, vehicleId, driverId, businessDate = plan.BusinessDate });
        create.EnsureSuccessStatusCode();
        var shipment = await create.DeserializeAsync<ShipmentDto>();
        shipment.Stops.Count.ShouldBe(2);
        shipment.Lines.Select(l => l.OrderId).Order().ShouldBe(new[] { orderA, orderB }.Order());
        using var load = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { toteIds = new[] { tote.Id } });
        load.EnsureSuccessStatusCode();
        using var depart = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart", new { });
        depart.EnsureSuccessStatusCode();
        return await depart.DeserializeAsync<ShipmentDto>();
    }
}
