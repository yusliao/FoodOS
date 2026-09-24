using System.Runtime.CompilerServices;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;
using FSH.Modules.Logistics.Contracts.v1.Drivers;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;
using FSH.Modules.Logistics.Contracts.v1.Vehicles;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Integration.Tests.Infrastructure;
using Mediator;

namespace Integration.Tests.Tests.Procurement;

[Collection(FshCollectionDefinition.Name)]
public sealed class ExternalWmsBoundaryTests(FshWebApplicationFactory factory)
{
    public static IEnumerable<object[]> BlockedMessages()
    {
        // Fail closed when a new command is added to an execution-adjacent module. A command must
        // be explicitly classified as FoodOS-owned master data / transaction maintenance below;
        // every other command is expected to hit the external-WMS boundary before its handler.
        var contractAssemblies = new[]
        {
            typeof(ReceiveInventoryCommand).Assembly,
            typeof(CreatePutawayTaskCommand).Assembly,
            typeof(PassQualityCheckCommand).Assembly,
            typeof(PlaceOrderCommand).Assembly,
            typeof(CreateShipmentCommand).Assembly,
        };
        var allowedFoodOsCommands = new HashSet<Type>
        {
            typeof(CreateWarehouseCommand),
            typeof(CreateSupplierCommand), typeof(CreatePurchaseOrderCommand),
            typeof(SendPurchaseOrderCommand), typeof(CreateInboundAppointmentCommand),
            typeof(UpdateCartCommand), typeof(UpdateShopCartCommand), typeof(PlaceShopOrderCommand),
            typeof(AmendShopOrderCommand), typeof(CancelShopOrderCommand),
            typeof(AmendOrderCommand), typeof(CancelOrderCommand),
            typeof(CreateStoreCommand), typeof(SetUserStoreAccessCommand),
            typeof(CreateCustomerOrgCommand), typeof(CreateAfterSalesTicketCommand),
            typeof(CreateShopAfterSalesCommand), typeof(ReconcileOrderCommand),
            typeof(CreateVehicleCommand), typeof(CreateDriverCommand), typeof(CreateRouteCommand),
        };
        return contractAssemblies.SelectMany(assembly => assembly.GetTypes()).Distinct()
            .Where(type => !allowedFoodOsCommands.Contains(type) && !type.IsAbstract && type.GetInterfaces()
            .Any(contract => contract == typeof(ICommand)
                || (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))))
            .Distinct().Select(type => new object[] { type });
    }

    [Theory]
    [MemberData(nameof(BlockedMessages))]
    public async Task Mediator_Should_RejectLegacyExecution_BeforeHandlers(Type messageType)
    {
        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        // Deliberately no HTTP context or tenant: background/internal dispatch must also fail closed.
        var command = RuntimeHelpers.GetUninitializedObject(messageType);
        var error = await Should.ThrowAsync<CustomException>(async () => await mediator.Send(command));
        error.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        error.Message.ShouldContain("External WMS confirmation");
    }

    [Theory]
    [InlineData("procurement/purchase-orders/00000000-0000-0000-0000-000000000001/lines/00000000-0000-0000-0000-000000000002/qc/pass")]
    [InlineData("procurement/purchase-orders/00000000-0000-0000-0000-000000000001/lines/00000000-0000-0000-0000-000000000002/qc/fail")]
    [InlineData("inventory/stock/receive")]
    [InlineData("logistics/shipments/00000000-0000-0000-0000-000000000001/depart")]
    public async Task RootHttp_Should_NotBypassExecutionBoundary(string path)
    {
        var auth = new AuthHelper(factory);
        using var client = await auth.CreateRootAdminClientAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/" + path, new {
            warehouseId = Guid.NewGuid(), productId = Guid.NewGuid(), zone = "Ambient",
            quantity = 5, sampleQty = 1, lotNo = "DENIED", expiryDate = "2027-01-01",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    [Fact]
    public async Task ReadAndSupplierMaintenance_Should_RemainAvailable()
    {
        var auth = new AuthHelper(factory);
        using var client = await auth.CreateRootAdminClientAsync();
        using var read = await client.GetAsync("/api/v1/procurement/purchase-orders");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var create = await client.PostAsJsonAsync("/api/v1/procurement/suppliers", new {
            code = "W" + Guid.NewGuid().ToString("N")[..8], name = "External WMS supplier", leadDays = 1,
        });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Capabilities_Should_RequireAuthentication_AndReportNotConfigured()
    {
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/v1/fulfillment/capabilities");
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        using var response = await client.GetAsync("/api/v1/fulfillment/capabilities");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using (body)
        {
            body.RootElement.GetProperty("mode").GetString().ShouldBe("externalWms");
            body.RootElement.GetProperty("readiness").GetString().ShouldBe("notConfigured");
            body.RootElement.GetProperty("acceptsOrders").GetBoolean().ShouldBeTrue();
            body.RootElement.GetProperty("acceptsOrderChanges").GetBoolean().ShouldBeTrue();
            body.RootElement.GetProperty("localWarehouseExecution").GetBoolean().ShouldBeFalse();
        }
    }
}
