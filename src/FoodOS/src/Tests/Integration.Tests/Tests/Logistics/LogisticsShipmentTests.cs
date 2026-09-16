using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Integration.Tests.Tests.Logistics;

/// <summary>
/// 剧本 A 步 7–9 + 剧本 D 步 2–3：装车发运、电子签收、对账、随车退、返仓待上架、报损入看板。
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class LogisticsShipmentTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public LogisticsShipmentTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task DepartAndFullPod_Should_MoveOrderToReconciled_WithLotOnOrder()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var packed = await PackOrderAsync(client, orderQty: 6m);

        var vehicleId = await CreateVehicleAsync(client);
        var driverId = await CreateDriverAsync(client);
        var routeId = await CreateRouteAsync(client, packed.WarehouseId, packed.StoreId);

        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId,
                warehouseId = packed.WarehouseId,
                vehicleId,
                driverId,
                businessDate = packed.BusinessDate
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var shipment = await create.DeserializeAsync<ShipmentDto>();
        shipment.Status.ShouldBe("Created");

        using var listShipments = await client.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments?warehouseId={packed.WarehouseId}");
        listShipments.StatusCode.ShouldBe(HttpStatusCode.OK, await listShipments.Content.ReadAsStringAsync());
        (await listShipments.DeserializeAsync<List<ShipmentDto>>())
            .ShouldContain(s => s.Id == shipment.Id);

        var line = shipment.Lines.ShouldHaveSingleItem();
        line.OrderId.ShouldBe(packed.OrderId);
        var lot = line.Lots.ShouldHaveSingleItem();
        lot.LotId.ShouldBe(packed.LotId);

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { packed.OrderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());
        (await load.DeserializeAsync<ShipmentDto>()).Status.ShouldBe("Loading");

        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());
        (await depart.DeserializeAsync<ShipmentDto>()).Status.ShouldBe("Departed");

        using var inTransit = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await inTransit.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("InTransit");

        var stop = shipment.Stops.ShouldHaveSingleItem();
        using var pod = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{stop.Id}/pod",
            new
            {
                lines = new[]
                {
                    new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = packed.Quantity }
                },
                signerName = "Chef Lee",
                photoFileIds = Array.Empty<Guid>(),
                geo = "42.36,-71.06"
            });
        pod.StatusCode.ShouldBe(HttpStatusCode.OK, await pod.Content.ReadAsStringAsync());
        var signed = await pod.DeserializeAsync<ShipmentDto>();
        signed.Status.ShouldBe("Completed");
        signed.Returns.ShouldBeEmpty();

        using var received = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        var receivedOrder = await received.DeserializeAsync<SalesOrderDto>();
        receivedOrder.Status.ShouldBe("Received");
        receivedOrder.Lines[0].DeliveredQty.ShouldBe(packed.Quantity);
        receivedOrder.Lines[0].Lots.ShouldHaveSingleItem().LotId.ShouldBe(packed.LotId);

        using var reconcile = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}/reconcile",
            new { });
        reconcile.StatusCode.ShouldBe(HttpStatusCode.OK, await reconcile.Content.ReadAsStringAsync());
        using var closed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await closed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Reconciled");

        using var claim = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/after-sales",
            new
            {
                orderId = packed.OrderId,
                orderLineId = receivedOrder.Lines[0].Id,
                type = "Return",
                quantity = 1m,
                reason = "bruised",
            });
        claim.StatusCode.ShouldBe(HttpStatusCode.OK, await claim.Content.ReadAsStringAsync());
        var ticket = await claim.DeserializeAsync<AfterSalesTicketDto>();
        ticket.Type.ShouldBe("Return");
        ticket.Status.ShouldBe("Applied");
        ticket.Quantity.ShouldBe(1m);

        using var listed = await client.GetAsync(
            $"{TestConstants.OrderingBasePath}/after-sales?storeId={packed.StoreId}");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        (await listed.DeserializeAsync<List<AfterSalesTicketDto>>())
            .ShouldContain(t => t.Id == ticket.Id);

        using var afterClaim = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        (await afterClaim.DeserializeAsync<SalesOrderDto>()).Lines[0].ReturnedQty.ShouldBe(1m);
    }

    [Fact]
    public async Task Driver_Should_Only_View_And_Confirm_AssignedShipment()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var role = await CreateRoleAsync(admin, $"DriverScope-{suffix}");
        await SetRolePermissionsAsync(
            admin,
            role.Id,
            LogisticsPermissions.Shipments.ViewAssigned,
            LogisticsPermissions.ProofOfDelivery.Confirm);

        var driverAUser = await CreateActiveUserAsync($"driver-a-{suffix}");
        var driverBUser = await CreateActiveUserAsync($"driver-b-{suffix}");
        await AssignRoleAsync(admin, driverAUser.UserId, role.Name);
        await AssignRoleAsync(admin, driverBUser.UserId, role.Name);
        using var driverA = await _auth.CreateAuthenticatedClientAsync(driverAUser.Email, driverAUser.Password);
        using var driverB = await _auth.CreateAuthenticatedClientAsync(driverBUser.Email, driverBUser.Password);

        var packed = await PackOrderAsync(admin, orderQty: 2m);
        var vehicleId = await CreateVehicleAsync(admin);
        var driverId = await CreateDriverAsync(admin, Guid.Parse(driverAUser.UserId));
        var routeId = await CreateRouteAsync(admin, packed.WarehouseId, packed.StoreId);
        using var create = await admin.PostAsJsonAsync($"{TestConstants.LogisticsBasePath}/shipments", new
        {
            routeId,
            warehouseId = packed.WarehouseId,
            vehicleId,
            driverId,
            businessDate = packed.BusinessDate,
        });
        var shipment = await create.DeserializeAsync<ShipmentDto>();
        var lot = shipment.Lines.ShouldHaveSingleItem().Lots.ShouldHaveSingleItem();
        var stop = shipment.Stops.ShouldHaveSingleItem();

        using var load = await admin.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { packed.OrderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());
        using var depart = await admin.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());

        using var assignedList = await driverA.GetAsync($"{TestConstants.LogisticsBasePath}/shipments/mine");
        assignedList.StatusCode.ShouldBe(HttpStatusCode.OK, await assignedList.Content.ReadAsStringAsync());
        (await assignedList.DeserializeAsync<IReadOnlyList<ShipmentDto>>()).ShouldContain(item => item.Id == shipment.Id);

        using var assignedDetail = await driverA.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/mine/{shipment.Id}");
        assignedDetail.StatusCode.ShouldBe(HttpStatusCode.OK, await assignedDetail.Content.ReadAsStringAsync());

        using var allShipmentDetail = await driverA.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}");
        allShipmentDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var foreignList = await driverB.GetAsync($"{TestConstants.LogisticsBasePath}/shipments/mine");
        foreignList.StatusCode.ShouldBe(HttpStatusCode.OK, await foreignList.Content.ReadAsStringAsync());
        (await foreignList.DeserializeAsync<IReadOnlyList<ShipmentDto>>()).ShouldNotContain(item => item.Id == shipment.Id);

        using var foreignDetail = await driverB.GetAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/mine/{shipment.Id}");
        foreignDetail.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignDetail.Content.ReadAsStringAsync());

        var signature = new
        {
            lines = new[] { new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = packed.Quantity } },
            signerName = "Assigned driver",
            photoFileIds = Array.Empty<Guid>(),
            geo = (string?)null,
        };
        using var foreignPod = await driverB.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{stop.Id}/pod",
            signature);
        foreignPod.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignPod.Content.ReadAsStringAsync());

        using var assignedPod = await driverA.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/stops/{stop.Id}/pod",
            signature);
        assignedPod.StatusCode.ShouldBe(HttpStatusCode.OK, await assignedPod.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialReject_Should_WriteReturnOnTruck_And_RestoreOnHand(bool failBeforeCompletion)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var packed = await PackOrderAsync(client, orderQty: 5m);
        decimal availableBefore = await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId);

        var vehicleId = await CreateVehicleAsync(client);
        var driverId = await CreateDriverAsync(client);
        var routeId = await CreateRouteAsync(client, packed.WarehouseId, packed.StoreId);

        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments",
            new
            {
                routeId,
                warehouseId = packed.WarehouseId,
                vehicleId,
                driverId,
                businessDate = packed.BusinessDate
            });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var shipment = await create.DeserializeAsync<ShipmentDto>();
        var lot = shipment.Lines.ShouldHaveSingleItem().Lots.ShouldHaveSingleItem();

        using var load = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/load",
            new { orderIds = new[] { packed.OrderId } });
        load.StatusCode.ShouldBe(HttpStatusCode.OK, await load.Content.ReadAsStringAsync());

        using var depart = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/shipments/{shipment.Id}/depart",
            new { });
        depart.StatusCode.ShouldBe(HttpStatusCode.OK, await depart.Content.ReadAsStringAsync());

        string podUrl = $"{TestConstants.LogisticsBasePath}/stops/{shipment.Stops[0].Id}/pod";
        var signature = new
            {
                lines = new[]
                {
                    new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = 3m }
                },
                signerName = "Chef Lee",
                photoFileIds = Array.Empty<Guid>(),
                geo = (string?)null
            };
        if (failBeforeCompletion)
        {
            var fault = new FailCompletionInterceptor();
            using var jobStorage = new JobStorageScope();
            using var failingFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddDbContext<LogisticsDbContext>(options => options.AddInterceptors(fault))));
            using var failingClient = failingFactory.CreateClient();
            failingClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            failingClient.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
            using var failed = await failingClient.PostAsJsonAsync(podUrl, signature);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            fault.Triggered.ShouldBeTrue();
            (await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId)).ShouldBe(availableBefore + 2m);

            using var changed = await client.PostAsJsonAsync(podUrl, new
            {
                lines = new[] { new { orderLineId = lot.OrderLineId, lotId = lot.LotId, signedQty = 4m } },
                signature.signerName,
                signature.photoFileIds,
                signature.geo
            });
            changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        using var pod = await client.PostAsJsonAsync(podUrl, signature);
        pod.StatusCode.ShouldBe(HttpStatusCode.OK, await pod.Content.ReadAsStringAsync());
        var signed = await pod.DeserializeAsync<ShipmentDto>();
        signed.Returns.ShouldHaveSingleItem().Quantity.ShouldBe(2m);

        using var received = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{packed.OrderId}");
        var order = await received.DeserializeAsync<SalesOrderDto>();
        order.Status.ShouldBe("Received");
        order.Lines[0].DeliveredQty.ShouldBe(3m);
        order.Lines[0].ReturnedQty.ShouldBe(2m);
        order.Lines[0].VarianceReason.ShouldBe("partial-reject");

        decimal availableAfter = await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId);
        availableAfter.ShouldBe(availableBefore + 2m);

        using var createPutaway = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/putaway-tasks",
            new
            {
                warehouseId = packed.WarehouseId,
                zone = "Ambient",
                productId = packed.ProductId,
                lotId = packed.LotId,
                quantity = 2m,
                source = "ReturnOnTruck"
            });
        createPutaway.StatusCode.ShouldBe(HttpStatusCode.OK, await createPutaway.Content.ReadAsStringAsync());
        var putaway = await createPutaway.DeserializeAsync<PutawayTaskDto>();
        putaway.Status.ShouldBe("Pending");
        putaway.Source.ShouldBe("ReturnOnTruck");

        using var shrink = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/shrinkage",
            new
            {
                warehouseId = packed.WarehouseId,
                zone = "Ambient",
                productId = packed.ProductId,
                lotId = packed.LotId,
                quantity = 2m,
                reason = "return-damage",
                photoFileIds = Array.Empty<Guid>()
            });
        shrink.StatusCode.ShouldBe(HttpStatusCode.OK, await shrink.Content.ReadAsStringAsync());
        (await GetAvailableAsync(client, packed.WarehouseId, packed.ProductId)).ShouldBe(availableBefore);

        using var kpis = await client.GetAsync(
            $"{TestConstants.OpsBasePath}/kpis?date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        kpis.StatusCode.ShouldBe(HttpStatusCode.OK, await kpis.Content.ReadAsStringAsync());
        var board = await kpis.DeserializeAsync<OpsKpisDto>();
        board.LossQty.ShouldBeGreaterThanOrEqualTo(2m);
        board.ShrinkageRate.ShouldBeGreaterThan(0m);
    }

    private sealed record PackedOrder(
        Guid WarehouseId,
        Guid StoreId,
        Guid ProductId,
        Guid OrderId,
        Guid LotId,
        DateOnly BusinessDate,
        decimal Quantity);

    private sealed class FailCompletionInterceptor : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && eventData.Context!.ChangeTracker.Entries<Shipment>()
                .Any(entry => entry.Entity.Status == ShipmentStatus.Completed))
            {
                Triggered = true;
                throw new InvalidOperationException("Injected failure before shipment completion.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private static async Task<PackedOrder> PackOrderAsync(HttpClient client, decimal orderQty)
    {
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid lotId = await ReceiveAsync(client, warehouse.Id, productId, $"LOT-{Guid.NewGuid():N}"[..12], 20m, today.AddDays(8));

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        await PutCartAsync(client, storeId, productId, orderQty);

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var cutoff = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff",
            new { });
        cutoff.StatusCode.ShouldBe(HttpStatusCode.OK, await cutoff.Content.ReadAsStringAsync());
        var cutoffResult = await cutoff.DeserializeAsync<CutoffResultDto>();

        using var generate = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = cutoffResult.BusinessDate });
        generate.StatusCode.ShouldBe(HttpStatusCode.OK, await generate.Content.ReadAsStringAsync());
        var wave = (await generate.DeserializeAsync<List<WaveDto>>()).ShouldHaveSingleItem();
        await WaveAssignments.AssignToSelfAsync(client, wave.Id);

        using var release = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{wave.Id}/release",
            new { });
        release.StatusCode.ShouldBe(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        var released = await release.DeserializeAsync<WaveDto>();
        var task = released.Tasks.ShouldHaveSingleItem();

        using var confirm = await client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{task.Id}/confirm",
            new { scannedLotId = lotId });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        using var packed = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        (await packed.DeserializeAsync<SalesOrderDto>()).Status.ShouldBe("Packed");

        return new PackedOrder(
            warehouse.Id, storeId, productId, orderId, lotId, cutoffResult.BusinessDate, orderQty);
    }

    private static async Task<Guid> CreateVehicleAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/vehicles",
            new
            {
                plate = $"P{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}",
                compartmentZones = "Ambient,Chilled",
                payloadKg = 3500m
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateDriverAsync(HttpClient client, Guid? userId = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/drivers",
            new { userId = userId ?? Guid.CreateVersion7(), phone = "+16175550100" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<RoleDto> CreateRoleAsync(HttpClient adminClient, string name)
    {
        using var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles", new
        {
            id = string.Empty,
            name,
            description = "driver assignment isolation test role",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<RoleDto>();
    }

    private static async Task SetRolePermissionsAsync(HttpClient adminClient, string roleId, params string[] permissions)
    {
        using var response = await adminClient.PutAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/{roleId}/permissions",
            new { roleId, permissions });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssignRoleAsync(HttpClient adminClient, string userId, string roleName)
    {
        using var response = await adminClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{userId}/roles",
            new { userId, userRoles = new[] { new { roleName, enabled = true } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<(string Email, string Password, string UserId)> CreateActiveUserAsync(string handle)
    {
        const string password = TestConstants.DefaultPassword;
        string email = $"{handle}@example.com";
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Driver",
            LastName = "Scope",
            Email = email,
            UserName = handle,
            EmailConfirmed = true,
            IsActive = true,
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Description)));
        return (email, password, user.Id);
    }

    private static async Task<Guid> CreateRouteAsync(HttpClient client, Guid warehouseId, Guid storeId)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.LogisticsBasePath}/routes",
            new
            {
                warehouseId,
                code = $"R{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                storeIds = new[] { storeId },
                defaultVehicleId = (Guid?)null
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task PutCartAsync(HttpClient client, Guid storeId, Guid productId, decimal qty)
    {
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = qty } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());
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

    private static async Task<Guid> CreateProductAsync(HttpClient client)
    {
        using var brandResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/brands",
            new { name = Unique("Brand"), description = (string?)null, logoUrl = (string?)null });
        brandResp.StatusCode.ShouldBe(HttpStatusCode.OK, await brandResp.Content.ReadAsStringAsync());

        using var categoryResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/categories",
            new { name = Unique("Cat"), description = (string?)null, parentCategoryId = (Guid?)null });
        categoryResp.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResp.Content.ReadAsStringAsync());

        var sku = $"LG-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name = Unique("Cod"),
                description = "Logistics SKU",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = 9.5m,
                priceCurrency = "USD",
                stock = 0,
            });
        productResp.StatusCode.ShouldBe(HttpStatusCode.OK, await productResp.Content.ReadAsStringAsync());
        return await productResp.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> ReceiveAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string lotNo,
        decimal quantity,
        DateOnly expiry)
    {
        using var receive = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/stock/receive",
            new
            {
                warehouseId,
                zone = "Ambient",
                productId,
                lotNo,
                expiryDate = expiry,
                quantity,
                idempotencyKey = $"recv-{Guid.NewGuid():N}",
                manufacturedOn = (DateOnly?)null,
                origin = "Boston",
            });
        receive.StatusCode.ShouldBe(HttpStatusCode.OK, await receive.Content.ReadAsStringAsync());
        return await receive.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateCustomerOrgAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/customer-orgs",
            new { code = $"C{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", name = Unique("Org") });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateStoreAsync(HttpClient client, Guid orgId, Guid warehouseId)
    {
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/stores",
            new
            {
                customerOrgId = orgId,
                code = $"S{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                name = Unique("Store"),
                address = "1 Harbor St",
                defaultWarehouseId = warehouseId,
                defaultRouteId = (Guid?)null,
                deliveryWindow = "05:00-08:00",
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<decimal> GetAvailableAsync(HttpClient client, Guid warehouseId, Guid productId)
    {
        using var response = await client.GetAsync(
            $"{TestConstants.InventoryBasePath}/stock/available?warehouseId={warehouseId}&productId={productId}&zone=Ambient");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.DeserializeAsync<AvailableQtyDto>()).Available;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
