using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Procurement;

/// <summary>
/// 剧本 B：采购建单预约 → 采购员不能质检入库（403）→ 不合格不增加 ATP 且 Lot Isolated → 合格增加 ATP。
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
    public async Task InboundQc_Should_SplitPurchaseAndQuality_And_KeepFailedLotOffAtp()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var warehouse = await CreateWarehouseAsync(admin);
        var supplierId = await CreateSupplierAsync(admin, unique);
        var failProductId = Guid.CreateVersion7();
        var passProductId = Guid.CreateVersion7();

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

        using var failQc = await inspector.PostAsJsonAsync(
            QcUrl(failPo.Id, failLineId, pass: false),
            QcBody("LOT-FAIL", 5m));
        failQc.StatusCode.ShouldBe(HttpStatusCode.OK, await failQc.Content.ReadAsStringAsync());

        var afterFail = await GetAvailableAsync(admin, warehouse.Id, failProductId, "Ambient");
        afterFail.Available.ShouldBe(beforeFail.Available);

        using var getFailedPo = await admin.GetAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{failPo.Id}");
        var failedPo = await getFailedPo.DeserializeAsync<PurchaseOrderDto>();
        var failedCheck = failedPo.QualityChecks.ShouldHaveSingleItem();
        failedCheck.Result.ShouldBe("Fail");
        failedCheck.LotId.ShouldNotBeNull();
        failedPo.Lines[0].RejectedQty.ShouldBe(5m);

        (await GetLotStatusAsync(failedCheck.LotId!.Value)).ShouldBe(LotStatus.Isolated);

        var passPo = await CreateAppointedPurchaseOrderAsync(
            purchaser, supplierId, warehouse.Id, passProductId, quantity: 7m);
        var passLineId = passPo.Lines.ShouldHaveSingleItem().Id;

        using var passQc = await inspector.PostAsJsonAsync(
            QcUrl(passPo.Id, passLineId, pass: true),
            QcBody("LOT-PASS", 7m));
        passQc.StatusCode.ShouldBe(HttpStatusCode.OK, await passQc.Content.ReadAsStringAsync());

        var afterPass = await GetAvailableAsync(admin, warehouse.Id, passProductId, "Ambient");
        afterPass.Available.ShouldBe(7m);
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

    private static string QcUrl(Guid purchaseOrderId, Guid lineId, bool pass)
        => $"{TestConstants.ProcurementBasePath}/purchase-orders/{purchaseOrderId}/lines/{lineId}/qc/{(pass ? "pass" : "fail")}";

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

    private async Task<LotStatus> GetLotStatusAsync(Guid lotId)
    {
        using var scope = _factory.Services.CreateScope();

        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var lot = await inventory.Lots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId);
        lot.ShouldNotBeNull($"Lot {lotId} was not found.");
        return lot.Status;
    }
}
