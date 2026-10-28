using FSH.Framework.Eventing.Abstractions;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    private const string OrderDeskBasePath = "/api/v1/ordering/order-desk";

    [Fact]
    public async Task OrderDesk_Should_RecognizeCaller_SuggestByPinyin_AndPlaceOrderForTheRestaurant()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantId = $"desk-{suffix}";
        string email = $"admin-{tenantId}@tenant.test";
        await CreateTenantAsync(rootClient, tenantId, email);
        await WaitForProvisioningAsync(rootClient, tenantId);
        var warehouse = await CreateWarehouseAsync(rootClient);
        Guid orgId = await CreateCustomerOrgAsync(rootClient, tenantId, $"D{suffix}");
        string storeCode = $"DK{suffix}".ToUpperInvariant();
        Guid storeId = await CreateStoreAsync(rootClient, orgId, warehouse.Id, storeCode);
        string sku = $"DESK-{suffix}".ToUpperInvariant();
        Guid productId = await CreateNamedProductAsync(rootClient, sku, $"冰鲜三文鱼刺身 {suffix}");

        // Caller contacts: phone numbers are matched on their digits.
        int exchange = System.Security.Cryptography.RandomNumberGenerator.GetInt32(200, 999);
        int line = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 9999);
        string phone = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"+1 (617) {exchange}-{line}");
        string phoneDigits = new(phone.Where(char.IsAsciiDigit).ToArray());
        using var contacts = await rootClient.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/stores/{storeId}/contacts",
            new { contacts = new[] { new { name = "Front desk", phone } } });
        contacts.StatusCode.ShouldBe(HttpStatusCode.OK, await contacts.Content.ReadAsStringAsync());
        using var duplicateContacts = await rootClient.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/stores/{storeId}/contacts",
            new { contacts = new[] { new { name = "A", phone }, new { name = "B", phone = phoneDigits } } });
        duplicateContacts.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var byPhone = await rootClient.GetAsync($"{OrderDeskBasePath}/callers?term={phoneDigits}");
        byPhone.StatusCode.ShouldBe(HttpStatusCode.OK, await byPhone.Content.ReadAsStringAsync());
        var caller = (await byPhone.DeserializeAsync<IReadOnlyList<OrderDeskCallerDto>>())
            .SingleOrDefault(item => item.StoreId == storeId).ShouldNotBeNull();
        caller.CanPlaceOrders.ShouldBeTrue();
        caller.CustomerOrgId.ShouldBe(orgId);
        caller.Contacts.ShouldHaveSingleItem().Phone.ShouldBe(phone);
        caller.LastOrderAt.ShouldBeNull();
        // suffix is lower-case hex, so this checks the match is case-insensitive.
        using var byCode = await rootClient.GetAsync($"{OrderDeskBasePath}/callers?term=dk{suffix}");
        (await byCode.DeserializeAsync<IReadOnlyList<OrderDeskCallerDto>>())
            .ShouldContain(item => item.StoreId == storeId);

        // Type-ahead: pinyin initials, full pinyin and exact SKU all find the product with the store price.
        foreach (string term in new[] { "bxswycs", "sanwenyu", "三文鱼", $"desk-{suffix}" })
        {
            using var suggest = await rootClient.GetAsync(
                $"{OrderDeskBasePath}/stores/{storeId}/products?term={Uri.EscapeDataString(term)}&limit=20");
            suggest.StatusCode.ShouldBe(HttpStatusCode.OK, await suggest.Content.ReadAsStringAsync());
            var candidates = await suggest.DeserializeAsync<IReadOnlyList<OrderDeskProductDto>>();
            var candidate = candidates.SingleOrDefault(item => item.ProductId == productId).ShouldNotBeNull($"term '{term}'");
            candidate.UnitPrice.ShouldBe(9.5m);
            candidate.Currency.ShouldBe("USD");
            candidate.AvailabilityStatus.ShouldBe("notConfigured");
        }

        using var exactSku = await rootClient.GetAsync($"{OrderDeskBasePath}/stores/{storeId}/products?term={sku}");
        (await exactSku.DeserializeAsync<IReadOnlyList<OrderDeskProductDto>>())[0].ProductId.ShouldBe(productId);

        using var quote = await rootClient.PostAsJsonAsync(
            $"{OrderDeskBasePath}/stores/{storeId}/quote",
            new { lines = new[] { new { productId, quantity = 3m } } });
        quote.StatusCode.ShouldBe(HttpStatusCode.OK, await quote.Content.ReadAsStringAsync());
        (await quote.DeserializeAsync<IReadOnlyList<OrderDeskQuoteLineDto>>())
            .ShouldHaveSingleItem().LineTotal.ShouldBe(28.5m);

        string key = Guid.NewGuid().ToString("N");
        using var placed = await PostOrderDeskOrderAsync(rootClient, storeId, key, productId, 3m);
        placed.StatusCode.ShouldBe(HttpStatusCode.OK, await placed.Content.ReadAsStringAsync());
        Guid orderId = await placed.DeserializeAsync<Guid>();
        using var repeated = await PostOrderDeskOrderAsync(rootClient, storeId, key, productId, 3m);
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK, await repeated.Content.ReadAsStringAsync());
        (await repeated.DeserializeAsync<Guid>()).ShouldBe(orderId);

        using var operatorOrder = await rootClient.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        operatorOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await operatorOrder.Content.ReadAsStringAsync());
        var order = await operatorOrder.DeserializeAsync<SalesOrderDto>();
        order.Status.ShouldBe("Reserved");
        order.PlacementChannel.ShouldBe("OrderDesk");
        order.CustomerTenantId.ShouldBe(tenantId.ToUpperInvariant());
        order.Lines.ShouldHaveSingleItem().UnitPrice.ShouldBe(9.5m);

        // The restaurant sees the order placed on its behalf, still waiting for the warehouse.
        using var customer = await CreateDashboardClientAsync(email, tenantId);
        await GrantSelfStoreAccessAsync(customer, storeId);
        using var shopOrder = await customer.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        shopOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await shopOrder.Content.ReadAsStringAsync());
        var customerView = await shopOrder.DeserializeAsync<ShopOrderDto>();
        customerView.PlacementChannel.ShouldBe("OrderDesk");
        customerView.WarehouseConfirmationStatus.ShouldBe("Pending");

        using var history = await rootClient.GetAsync($"{OrderDeskBasePath}/stores/{storeId}/history");
        history.StatusCode.ShouldBe(HttpStatusCode.OK, await history.Content.ReadAsStringAsync());
        var storeHistory = await history.DeserializeAsync<OrderDeskStoreHistoryDto>();
        var recent = storeHistory.RecentOrders.ShouldHaveSingleItem();
        recent.Id.ShouldBe(orderId);
        recent.TotalAmount.ShouldBe(28.5m);
        var recentLine = recent.Lines.ShouldHaveSingleItem();
        recentLine.Sku.ShouldBe(sku);
        recentLine.Name.ShouldStartWith("冰鲜三文鱼刺身");
        var frequent = storeHistory.FrequentProducts.ShouldHaveSingleItem();
        frequent.ProductId.ShouldBe(productId);
        frequent.OrderCount.ShouldBe(1);
        frequent.LastQty.ShouldBe(3m);

        using var callerAfter = await rootClient.GetAsync($"{OrderDeskBasePath}/callers?term={phoneDigits}");
        (await callerAfter.DeserializeAsync<IReadOnlyList<OrderDeskCallerDto>>())
            .Single(item => item.StoreId == storeId).LastOrderAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task OrderDesk_Should_BeLimitedToCustomerServicePermissions()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantId = $"desk-perm-{suffix}";
        string email = $"admin-{tenantId}@tenant.test";
        await CreateTenantAsync(rootClient, tenantId, email);
        await WaitForProvisioningAsync(rootClient, tenantId);
        var warehouse = await CreateWarehouseAsync(rootClient);
        Guid orgId = await CreateCustomerOrgAsync(rootClient, tenantId, $"P{suffix}");
        Guid storeId = await CreateStoreAsync(rootClient, orgId, warehouse.Id, $"DP{suffix}".ToUpperInvariant());
        Guid productId = await CreateProductAsync(rootClient);

        // A restaurant account can never use the operator order desk.
        using var customer = await CreateDashboardClientAsync(email, tenantId);
        using var customerCallers = await customer.GetAsync($"{OrderDeskBasePath}/callers?term=DP");
        customerCallers.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var customerPlace = await PostOrderDeskOrderAsync(customer, storeId, Guid.NewGuid().ToString("N"), productId, 1m);
        customerPlace.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Operator staff without a position cannot either.
        var (staffEmail, staffPassword, _) = await CreateRootStaffAsync($"desk-none-{suffix}", roleName: null);
        using var staff = await _auth.CreateAuthenticatedClientAsync(staffEmail, staffPassword);
        using var staffCallers = await staff.GetAsync($"{OrderDeskBasePath}/callers?term=DP");
        staffCallers.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The seeded customer service position can take the order end to end.
        var (agentEmail, agentPassword, agentId) = await CreateRootStaffAsync(
            $"desk-agent-{suffix}", OrderingRoleTemplates.CustomerServiceRole);
        using var agent = await _auth.CreateAuthenticatedClientAsync(agentEmail, agentPassword);
        using var agentCallers = await agent.GetAsync($"{OrderDeskBasePath}/callers?term=DP{suffix}");
        agentCallers.StatusCode.ShouldBe(HttpStatusCode.OK, await agentCallers.Content.ReadAsStringAsync());
        using var agentPlace = await PostOrderDeskOrderAsync(agent, storeId, Guid.NewGuid().ToString("N"), productId, 2m);
        agentPlace.StatusCode.ShouldBe(HttpStatusCode.OK, await agentPlace.Content.ReadAsStringAsync());
        Guid orderId = await agentPlace.DeserializeAsync<Guid>();

        // ...and amend it before cutoff, but it cannot maintain store master data.
        using var agentOrder = await agent.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        agentOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await agentOrder.Content.ReadAsStringAsync());
        var amendRequest = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend")
        {
            Content = JsonContent.Create(new { orderId, lines = new[] { new { productId, quantity = 5m } } }),
        };
        amendRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var amend = await agent.SendAsync(amendRequest);
        amend.StatusCode.ShouldBe(HttpStatusCode.OK, await amend.Content.ReadAsStringAsync());
        using var agentContacts = await agent.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/stores/{storeId}/contacts",
            new { contacts = Array.Empty<object>() });
        agentContacts.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using (_factory.Services.GetRequiredService<IEventTenantScope>().Begin(TestConstants.RootTenantId))
        using (var scope = _factory.Services.CreateScope())
        {
            var ordering = scope.ServiceProvider.GetRequiredService<FSH.Modules.Ordering.Data.OrderingDbContext>();
            var saved = await ordering.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == orderId);
            saved.PlacedByUserId.ShouldBe(Guid.Parse(agentId));
        }
    }

    private static Task<HttpResponseMessage> PostOrderDeskOrderAsync(
        HttpClient client,
        Guid storeId,
        string idempotencyKey,
        Guid productId,
        decimal quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{OrderDeskBasePath}/stores/{storeId}/orders")
        {
            Content = JsonContent.Create(new { lines = new[] { new { productId, quantity } } }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static async Task<Guid> CreateNamedProductAsync(HttpClient client, string sku, string name)
    {
        using var brand = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/brands",
            new { name = $"Brand-{Guid.NewGuid():N}", description = (string?)null, logoUrl = (string?)null });
        brand.EnsureSuccessStatusCode();
        using var category = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/categories",
            new { name = $"Category-{Guid.NewGuid():N}", description = (string?)null, parentCategoryId = (Guid?)null });
        category.EnsureSuccessStatusCode();
        using var product = await client.PostAsJsonAsync($"{TestConstants.CatalogBasePath}/products", new
        {
            sku,
            name,
            description = "Order desk product",
            brandId = await brand.DeserializeAsync<Guid>(),
            categoryId = await category.DeserializeAsync<Guid>(),
            priceAmount = 9.5m,
            priceCurrency = "USD",
            stock = 0,
        });
        product.StatusCode.ShouldBe(HttpStatusCode.OK, await product.Content.ReadAsStringAsync());
        return await product.DeserializeAsync<Guid>();
    }

    private async Task<(string Email, string Password, string UserId)> CreateRootStaffAsync(string handle, string? roleName)
    {
        using var tenantScope = _factory.Services.GetRequiredService<IEventTenantScope>().Begin(TestConstants.RootTenantId);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Desk",
            LastName = "Probe",
            Email = $"{handle}@example.com",
            UserName = handle,
            EmailConfirmed = true,
            IsActive = true,
        };
        var result = await userManager.CreateAsync(user, TestConstants.DefaultPassword);
        result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Description)));
        if (roleName is not null)
        {
            var assigned = await userManager.AddToRoleAsync(user, roleName);
            assigned.Succeeded.ShouldBeTrue(string.Join(", ", assigned.Errors.Select(error => error.Description)));
        }

        return (user.Email, TestConstants.DefaultPassword, user.Id);
    }
}
