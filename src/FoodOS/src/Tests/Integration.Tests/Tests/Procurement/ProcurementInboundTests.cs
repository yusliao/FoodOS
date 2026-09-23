using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Procurement;

/// <summary>
/// 外部 WMS 模式：FoodOS 保留采购与到货预约，但本地质检收货必须失败关闭且不产生库存副作用。
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class ProcurementInboundTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public ProcurementInboundTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task InboundQc_Should_KeepRoleSeparation_AndFailClosedWithoutInventorySideEffects()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var warehouse = await CreateWarehouseAsync(admin);
        var supplierId = await CreateSupplierAsync(admin, unique);
        var failProductId = Guid.CreateVersion7();

        var purchaserRole = await CreateRoleAsync(admin, $"Purchaser-{unique}");
        await SetRolePermissionsAsync(
            admin,
            purchaserRole.Id,
            ProcurementPermissions.Purchase.View,
            ProcurementPermissions.Purchase.Create,
            ProcurementPermissions.Suppliers.View,
            ProcurementPermissions.Suppliers.Create);

        var (purchaserEmail, purchaserPassword, purchaserId) = await CreateActiveUserAsync($"purchaser-{unique}");
        await AssignRoleAsync(admin, purchaserId, purchaserRole.Name);
        using var purchaser = await _auth.CreateAuthenticatedClientAsync(purchaserEmail, purchaserPassword);

        var qcRole = await CreateRoleAsync(admin, $"Qc-{unique}");
        await SetRolePermissionsAsync(
            admin,
            qcRole.Id,
            ProcurementPermissions.Purchase.View,
            ProcurementPermissions.Quality.View,
            ProcurementPermissions.Quality.Pass,
            ProcurementPermissions.Quality.Fail);

        var (qcEmail, qcPassword, qcId) = await CreateActiveUserAsync($"qc-{unique}");
        await AssignRoleAsync(admin, qcId, qcRole.Name);
        using var inspector = await _auth.CreateAuthenticatedClientAsync(qcEmail, qcPassword);

        var failPo = await CreateAppointedPurchaseOrderAsync(
            purchaser, supplierId, warehouse.Id, failProductId, quantity: 5m);
        failPo.Status.ShouldBe("Receiving");
        failPo.Appointment.ShouldNotBeNull();
        var failLineId = failPo.Lines.ShouldHaveSingleItem().Id;

        var beforeFail = await GetAvailableAsync(admin, warehouse.Id, failProductId, "Ambient");
        beforeFail.Available.ShouldBe(0m);

        using var purchaserPass = await purchaser.PostAsJsonAsync(
            QcUrl(failPo.Id, failLineId, pass: true),
            QcBody("LOT-FAIL", 5m));
        purchaserPass.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await purchaserPass.Content.ReadAsStringAsync());

        using var purchaserFail = await purchaser.PostAsJsonAsync(
            QcUrl(failPo.Id, failLineId, pass: false), QcBody("LOT-FAIL", 5m));
        purchaserFail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await AssertProcurementWritesForbiddenAsync(inspector, supplierId, warehouse.Id, failProductId, failPo.Id);

        using var failQc = await inspector.PostAsJsonAsync(
            QcUrl(failPo.Id, failLineId, pass: false),
            QcBody("LOT-FAIL", 5m));
        failQc.StatusCode.ShouldBe(HttpStatusCode.Conflict, await failQc.Content.ReadAsStringAsync());
        (await failQc.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");

        var afterFail = await GetAvailableAsync(admin, warehouse.Id, failProductId, "Ambient");
        afterFail.Available.ShouldBe(beforeFail.Available);

        using var getFailedPo = await admin.GetAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{failPo.Id}");
        var failedPo = await getFailedPo.DeserializeAsync<PurchaseOrderDto>();
        failedPo.QualityChecks.ShouldBeEmpty();
        failedPo.Lines[0].ReceivedQty.ShouldBe(0m);
        failedPo.Lines[0].RejectedQty.ShouldBe(0m);

        using var passQc = await inspector.PostAsJsonAsync(
            QcUrl(failPo.Id, failLineId, pass: true),
            QcBody("LOT-PASS", 5m));
        passQc.StatusCode.ShouldBe(HttpStatusCode.Conflict, await passQc.Content.ReadAsStringAsync());
        (await passQc.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
        (await GetAvailableAsync(admin, warehouse.Id, failProductId, "Ambient")).Available.ShouldBe(0m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepeatedLotQualityCheck_Should_RemainBlockedWithoutReceipts(bool pass)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var supplierId = await CreateSupplierAsync(client, Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.CreateVersion7();
        var po = await CreateAppointedPurchaseOrderAsync(client, supplierId, warehouse.Id, productId, 10m);
        var lineId = po.Lines[0].Id;
        using var first = await PostQualityWithFreshKeyAsync(client, QcUrl(po.Id, lineId, pass), "REPLAY-LOT", 5m);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict, await first.Content.ReadAsStringAsync());

        using var repeated = await PostQualityWithFreshKeyAsync(client, QcUrl(po.Id, lineId, pass), " replay-lot ", 5m);
        repeated.StatusCode.ShouldBe(HttpStatusCode.Conflict, await repeated.Content.ReadAsStringAsync());
        using var changed = await PostQualityWithFreshKeyAsync(client, QcUrl(po.Id, lineId, pass), "REPLAY-LOT", 3m);
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changed.Content.ReadAsStringAsync());

        using var get = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var after = await get.DeserializeAsync<PurchaseOrderDto>();
        after.QualityChecks.ShouldBeEmpty();
        after.Lines[0].ReceivedQty.ShouldBe(0m);
        after.Lines[0].RejectedQty.ShouldBe(0m);
        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await inventory.LotBalances.AnyAsync(b => b.ProductId == productId)).ShouldBeFalse();
        var tasks = scope.ServiceProvider.GetRequiredService<FSH.Modules.Warehouse.Data.WarehouseDbContext>();
        (await tasks.PutawayTasks.AnyAsync(t => t.ProductId == productId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DraftPurchaseOrder_QualityCheck_Should_NotCreateInventory(bool pass)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var supplierId = await CreateSupplierAsync(client, Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.CreateVersion7();
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders",
            new { supplierId, warehouseId = warehouse.Id, expectedAt = DateTimeOffset.UtcNow.AddDays(1),
                lines = new[] { new { productId, zone = "Ambient", quantity = 4m } } });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();
        using var get = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{id}");
        var po = await get.DeserializeAsync<PurchaseOrderDto>();
        using var response = await client.PostAsJsonAsync(QcUrl(id, po.Lines[0].Id, pass), QcBody("DRAFT-QC", 4m));
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await inventory.InventoryTransactions.AnyAsync(t => t.ProductId == productId)).ShouldBeFalse();
        (await inventory.Lots.AnyAsync(l => l.ProductId == productId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ConcurrentLotQualityChecks_Should_AllFailClosedWithoutStock(bool pass, bool sameLot)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var supplierId = await CreateSupplierAsync(client, Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.CreateVersion7();
        var po = await CreateAppointedPurchaseOrderAsync(client, supplierId, warehouse.Id, productId, 10m);
        string url = QcUrl(po.Id, po.Lines[0].Id, pass);
        var responses = await Task.WhenAll(
            PostQualityWithFreshKeyAsync(client, url, "CONCURRENT-A", 5m),
            PostQualityWithFreshKeyAsync(client, url, sameLot ? " concurrent-a " : "CONCURRENT-B", 5m));
        try
        {
            responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        using var get = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var after = await get.DeserializeAsync<PurchaseOrderDto>();
        after.QualityChecks.ShouldBeEmpty();
        after.Lines[0].ReceivedQty.ShouldBe(0m);
        after.Lines[0].RejectedQty.ShouldBe(0m);
        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);

        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await inventory.LotBalances.AnyAsync(balance => balance.ProductId == productId)).ShouldBeFalse();
        var procurement = scope.ServiceProvider.GetRequiredService<FSH.Modules.Procurement.Data.ProcurementDbContext>();
        (await procurement.ReceiveRecords.AnyAsync(record => record.PurchaseOrderId == po.Id)).ShouldBeFalse();
        (await procurement.TraceEvents.AnyAsync(record => record.ProductId == productId)).ShouldBeFalse();
        var tasks = scope.ServiceProvider.GetRequiredService<FSH.Modules.Warehouse.Data.WarehouseDbContext>();
        (await tasks.PutawayTasks.AnyAsync(task => task.ProductId == productId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RestaurantAdmin_Should_NotReadOrMutateOperatorProcurement()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var warehouse = await CreateWarehouseAsync(admin);
        var supplierId = await CreateSupplierAsync(admin, unique);
        var productId = Guid.CreateVersion7();
        var po = await CreateAppointedPurchaseOrderAsync(admin, supplierId, warehouse.Id, productId, 5m);
        var tenantId = $"proc-{unique}";
        var email = $"admin@{tenantId}.example.com";
        using var create = await admin.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId, name = $"Restaurant {unique}", connectionString = (string?)null,
            adminEmail = email, adminPassword = TestConstants.DefaultPassword, issuer = $"{tenantId}.issuer",
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        await WaitForProvisioningAsync(admin, tenantId);
        using var customer = await _auth.CreateAuthenticatedClientAsync(email, TestConstants.DefaultPassword, tenantId);

        foreach (var path in new[] { "suppliers", $"suppliers/{supplierId}", "purchase-orders", $"purchase-orders/{po.Id}" })
        {
            using var response = await customer.GetAsync($"{TestConstants.ProcurementBasePath}/{path}");
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"Customer read {path}: {await response.Content.ReadAsStringAsync()}");
        }

        await AssertProcurementWritesForbiddenAsync(customer, supplierId, warehouse.Id, productId, po.Id);
        foreach (var pass in new[] { true, false })
        {
            using var response = await customer.PostAsJsonAsync(
                QcUrl(po.Id, po.Lines[0].Id, pass), QcBody($"LOT-{unique}", 5m));
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var get = await admin.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var unchanged = await get.DeserializeAsync<PurchaseOrderDto>();
        unchanged.Status.ShouldBe(po.Status);
        unchanged.QualityChecks.ShouldBeEmpty();
        (await GetAvailableAsync(admin, warehouse.Id, productId, "Ambient")).Available.ShouldBe(0m);
    }

    private static async Task AssertProcurementWritesForbiddenAsync(
        HttpClient client, Guid supplierId, Guid warehouseId, Guid productId, Guid purchaseOrderId)
    {
        var requests = new (string Path, object Body)[]
        {
            ("suppliers", new { code = $"DENY{Guid.NewGuid():N}", name = "Denied supplier", categories = "produce", leadDays = 2 }),
            ("purchase-orders", new { supplierId, warehouseId, expectedAt = DateTimeOffset.UtcNow.AddDays(1),
                lines = new[] { new { productId, zone = "Ambient", quantity = 5m } } }),
            ($"purchase-orders/{purchaseOrderId}/send", new { }),
            ($"purchase-orders/{purchaseOrderId}/appointments", new { dockSlot = "DENIED", vehicleNo = "DENIED" }),
        };
        foreach (var (path, body) in requests)
        {
            using var response = await client.PostAsJsonAsync($"{TestConstants.ProcurementBasePath}/{path}", body);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"Write {path}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task WaitForProvisioningAsync(HttpClient admin, string tenantId)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var response = await admin.GetAsync($"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
            response.EnsureSuccessStatusCode();
            var status = (await response.DeserializeAsync<TenantProvisioningStatusDto>()).Status;
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Tenant {tenantId} provisioning failed.");
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Tenant {tenantId} did not finish provisioning.");
    }

    private static string QcUrl(Guid purchaseOrderId, Guid lineId, bool pass)
        => $"{TestConstants.ProcurementBasePath}/purchase-orders/{purchaseOrderId}/lines/{lineId}/qc/{(pass ? "pass" : "fail")}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockedQualityCheck_Should_NotCreateReceiptOnRetry(bool pass)
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(admin);
        var supplier = await CreateSupplierAsync(admin, Guid.NewGuid().ToString("N")[..8]);
        var product = Guid.CreateVersion7();
        var po = await CreateAppointedPurchaseOrderAsync(admin, supplier, warehouse.Id, product, 10m);
        string url = QcUrl(po.Id, po.Lines[0].Id, pass);
        using var first = await PostQualityWithFreshKeyAsync(admin, url, "RECOVER-LOT", 5m);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict, await first.Content.ReadAsStringAsync());
        using var interruptedGet = await admin.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var interrupted = await interruptedGet.DeserializeAsync<PurchaseOrderDto>();
        interrupted.QualityChecks.ShouldBeEmpty();

        using var changed = await PostQualityWithFreshKeyAsync(admin, url, "RECOVER-LOT", 3m);
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changed.Content.ReadAsStringAsync());
        using var retry = await PostQualityWithFreshKeyAsync(admin, url, "RECOVER-LOT", 5m);
        retry.StatusCode.ShouldBe(HttpStatusCode.Conflict, await retry.Content.ReadAsStringAsync());
        using var get = await admin.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{po.Id}");
        var after = await get.DeserializeAsync<PurchaseOrderDto>();
        after.QualityChecks.ShouldBeEmpty();
        after.Lines[0].ReceivedQty.ShouldBe(0m);
        after.Lines[0].RejectedQty.ShouldBe(0m);
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        (await inventory.LotBalances.AnyAsync(b => b.ProductId == product)).ShouldBeFalse();
        var tasks = scope.ServiceProvider.GetRequiredService<FSH.Modules.Warehouse.Data.WarehouseDbContext>();
        (await tasks.PutawayTasks.AnyAsync(task => task.ProductId == product)).ShouldBeFalse();
    }

    private static async Task<HttpResponseMessage> PostQualityWithFreshKeyAsync(HttpClient client, string url, string lotNo, decimal quantity)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(QcBody(lotNo, quantity)) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static object QcBody(string lotNo, decimal quantity) => new
    {
        quantity,
        sampleQty = 1m,
        lotNo,
        expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        manufacturedOn = (DateOnly?)null,
        note = (string?)null,
        photoFileIds = (IReadOnlyList<Guid>?)null,
    };

    private static async Task<PurchaseOrderDto> CreateAppointedPurchaseOrderAsync(
        HttpClient client,
        Guid supplierId,
        Guid warehouseId,
        Guid productId,
        decimal quantity)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders",
            new
            {
                supplierId,
                warehouseId,
                expectedAt = DateTimeOffset.UtcNow.AddDays(1),
                lines = new[]
                {
                    new { productId, zone = "Ambient", quantity }
                }
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var poId = await create.DeserializeAsync<Guid>();

        using var appoint = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}/appointments",
            new { dockSlot = "DOCK-A", vehicleNo = "TRK-1" });
        appoint.StatusCode.ShouldBe(HttpStatusCode.OK, await appoint.Content.ReadAsStringAsync());

        using var get = await client.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK, await get.Content.ReadAsStringAsync());
        return await get.DeserializeAsync<PurchaseOrderDto>();
    }

    private static async Task<Guid> CreateSupplierAsync(HttpClient client, string unique)
    {
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/suppliers",
            new { code = $"SUP{unique[..6]}", name = $"Supplier {unique}", categories = "produce", leadDays = 2 });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        return await create.DeserializeAsync<Guid>();
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        var code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();

        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }

    private static async Task<AvailableQtyDto> GetAvailableAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string zone)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone={zone}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<AvailableQtyDto>();
    }

    private static async Task<RoleDto> CreateRoleAsync(HttpClient adminClient, string name)
    {
        var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles", new
        {
            id = string.Empty,
            name,
            description = "procurement inbound test role"
        });
        return await response.DeserializeAsync<RoleDto>();
    }

    private static async Task SetRolePermissionsAsync(HttpClient adminClient, string roleId, params string[] permissions)
    {
        var response = await adminClient.PutAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/{roleId}/permissions", new
            {
                roleId,
                permissions
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"Set role permissions failed: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task AssignRoleAsync(HttpClient adminClient, string userId, string roleName)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{userId}/roles", new
            {
                userId,
                userRoles = new[]
                {
                    new { roleName, enabled = true }
                }
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"Assign role failed: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<(string Email, string Password, string UserId)> CreateActiveUserAsync(string handle)
    {
        const string password = TestConstants.DefaultPassword;
        var email = $"{handle}@example.com";

        using var scope = _factory.Services.CreateScope();

        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Proc",
            LastName = "Probe",
            Email = email,
            UserName = handle,
            EmailConfirmed = true,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(
            $"Seeding active user failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return (email, password, user.Id);
    }

}
