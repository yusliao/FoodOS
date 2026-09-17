using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;
using FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;
using FSH.Framework.Core.Exceptions;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using Mediator;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

/// <summary>
/// Shop path: org/store → cart → place (ReserveStock) → amend/cancel before cutoff.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class OrderingShopTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OrderingShopTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task CancelledPlacement_Should_ReleaseStock_And_PreserveCart()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-CANCEL", 10m);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 6m } } });
        putCart.EnsureSuccessStatusCode();

        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelPlacementInterceptor(cancellation);
        using var jobStorage = new JobStorageScope();
        using var failingFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDbContext<OrderingDbContext>(options => options.AddInterceptors(interceptor))));
        using var scope = failingFactory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var handler = new PlaceOrderCommandHandler(db, scope.ServiceProvider.GetRequiredService<IMediator>(), TimeProvider.System);
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await handler.Handle(new PlaceOrderCommand(storeId), cancellation.Token));

        interceptor.Triggered.ShouldBeTrue();
        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(10m);
        using var getCart = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        (await getCart.DeserializeAsync<CartDto>()).Lines.ShouldHaveSingleItem().Quantity.ShouldBe(6m);
        db.ChangeTracker.Clear();
        var order = await db.SalesOrders.SingleAsync(o => o.StoreId == storeId);
        order.Status.ShouldBe(SalesOrderStatus.Cancelled);
        order.Lines.ShouldAllBe(line => line.ReservationId == null);
    }

    private sealed class CancelPlacementInterceptor(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && eventData.Context!.ChangeTracker.Entries<SalesOrder>()
                .Any(entry => entry.Entity.Status == SalesOrderStatus.Reserved))
            {
                Triggered = true;
                await cancellation.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return result;
        }
    }

    [Fact]
    public async Task PlaceAmendCancel_Should_ReserveAndReleaseAtp()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-SO1", 10m);

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 6m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.OK, await place.Content.ReadAsStringAsync());
        var orderId = await place.DeserializeAsync<Guid>();

        using var getPlaced = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var placed = await getPlaced.DeserializeAsync<SalesOrderDto>();
        placed.Status.ShouldBe("Reserved");
        placed.Lines.Count.ShouldBe(1);
        placed.Lines[0].OrderedQty.ShouldBe(6m);
        placed.Lines[0].ReservationId.ShouldNotBeNull();

        var afterPlace = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterPlace.Available.ShouldBe(4m);

        using var getCart = await client.GetAsync($"{TestConstants.OrderingBasePath}/carts/{storeId}");
        var cart = await getCart.DeserializeAsync<CartDto>();
        cart.Lines.ShouldBeEmpty();

        using var amend = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/amend",
            new { orderId, lines = new[] { new { productId, quantity = 3m } } });
        amend.StatusCode.ShouldBe(HttpStatusCode.OK, await amend.Content.ReadAsStringAsync());

        using var getAmended = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var amended = await getAmended.DeserializeAsync<SalesOrderDto>();
        amended.Status.ShouldBe("Reserved");
        amended.Revision.ShouldBe(1);
        amended.Lines.ShouldHaveSingleItem().OrderedQty.ShouldBe(3m);

        var afterAmend = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterAmend.Available.ShouldBe(7m);

        using var cancel = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderId}/cancel",
            new { });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        using var getCancelled = await client.GetAsync($"{TestConstants.OrderingBasePath}/orders/{orderId}");
        var cancelled = await getCancelled.DeserializeAsync<SalesOrderDto>();
        cancelled.Status.ShouldBe("Cancelled");

        var afterCancel = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        afterCancel.Available.ShouldBe(10m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedCancellation_Should_PreserveReservation(bool afterCutoff)
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-REJECT-CANCEL", 10m);
        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);
        using var cart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 6m } } });
        cart.EnsureSuccessStatusCode();
        using var place = await client.PostAsJsonAsync($"{TestConstants.OrderingBasePath}/orders", new { storeId });
        place.EnsureSuccessStatusCode();
        var orderId = await place.DeserializeAsync<Guid>();

        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var order = await db.SalesOrders.SingleAsync(o => o.Id == orderId);
        var reservationId = order.Lines.Single().ReservationId;
        if (!afterCutoff)
        {
            order.LockForCutoff();
            await db.SaveChangesAsync();
        }

        var clock = new CancellationClock(afterCutoff ? order.CutoffAt.AddSeconds(1) : order.CutoffAt.AddSeconds(-1));
        var handler = new CancelOrderCommandHandler(db, scope.ServiceProvider.GetRequiredService<IMediator>(), clock);
        await Should.ThrowAsync<CustomException>(async () => await handler.Handle(new CancelOrderCommand(orderId), CancellationToken.None));

        (await GetAvailableAsync(client, warehouse.Id, productId, "Ambient")).Available.ShouldBe(4m);
        db.ChangeTracker.Clear();
        var persisted = await db.SalesOrders.SingleAsync(o => o.Id == orderId);
        persisted.Status.ShouldBe(afterCutoff ? SalesOrderStatus.Reserved : SalesOrderStatus.Planned);
        persisted.Lines.Single().ReservationId.ShouldBe(reservationId);
        persisted.Lines.Single().ReservedQty.ShouldBe(6m);
    }

    private sealed class CancellationClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task PlaceOrder_Should_Conflict_When_InsufficientAtp()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var productId = await CreateProductAsync(client);
        await ReceiveAsync(client, warehouse.Id, productId, "LOT-SO2", 2m);

        var orgId = await CreateCustomerOrgAsync(client);
        var storeId = await CreateStoreAsync(client, orgId, warehouse.Id);

        using var putCart = await client.PutAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/carts/{storeId}",
            new { storeId, lines = new[] { new { productId, quantity = 5m } } });
        putCart.StatusCode.ShouldBe(HttpStatusCode.OK, await putCart.Content.ReadAsStringAsync());

        using var place = await client.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders",
            new { storeId });
        place.StatusCode.ShouldBe(HttpStatusCode.Conflict, await place.Content.ReadAsStringAsync());

        var available = await GetAvailableAsync(client, warehouse.Id, productId, "Ambient");
        available.Available.ShouldBe(2m);
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

        var sku = $"SO-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var productResp = await client.PostAsJsonAsync(
            $"{TestConstants.CatalogBasePath}/products",
            new
            {
                sku,
                name = Unique("Cod"),
                description = "Shop SKU",
                brandId = await brandResp.DeserializeAsync<Guid>(),
                categoryId = await categoryResp.DeserializeAsync<Guid>(),
                priceAmount = 9.5m,
                priceCurrency = "USD",
                stock = 0,
            });
        productResp.StatusCode.ShouldBe(HttpStatusCode.OK, await productResp.Content.ReadAsStringAsync());
        return await productResp.DeserializeAsync<Guid>();
    }

    private static async Task ReceiveAsync(
        HttpClient client,
        Guid warehouseId,
        Guid productId,
        string lotNo,
        decimal quantity)
    {
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
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

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
