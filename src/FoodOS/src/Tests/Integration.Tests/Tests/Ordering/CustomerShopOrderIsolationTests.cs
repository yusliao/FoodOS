using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    [Fact]
    public async Task RestaurantCustomer_Should_NotReadOrWrite_AnotherCustomersOrdersAndAfterSales()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantA = $"order-a-{suffix}";
        string tenantB = $"order-b-{suffix}";
        string emailA = $"admin-{tenantA}@tenant.test";
        string emailB = $"admin-{tenantB}@tenant.test";
        await CreateTenantAsync(rootClient, tenantA, emailA);
        await CreateTenantAsync(rootClient, tenantB, emailB);
        await WaitForProvisioningAsync(rootClient, tenantA);
        await WaitForProvisioningAsync(rootClient, tenantB);

        var warehouse = await CreateWarehouseAsync(rootClient);
        Guid productId = await CreateProductAsync(rootClient);
        Guid orgA = await CreateCustomerOrgAsync(rootClient, tenantA, $"OA{suffix}");
        Guid orgB = await CreateCustomerOrgAsync(rootClient, tenantB, $"OB{suffix}");
        Guid storeA = await CreateStoreAsync(rootClient, orgA, warehouse.Id, $"OSA{suffix}");
        Guid storeB = await CreateStoreAsync(rootClient, orgB, warehouse.Id, $"OSB{suffix}");

        using var clientA = await CreateDashboardClientAsync(emailA, tenantA);
        using var clientB = await CreateDashboardClientAsync(emailB, tenantB);
        await GrantSelfStoreAccessAsync(clientA, storeA);
        await GrantSelfStoreAccessAsync(clientB, storeB);

        (Guid orderId, Guid orderLineId, Guid ticketId) = await SeedOrderAndAfterSalesAsync(
            tenantB, orgB, storeB, warehouse.Id, productId);

        using var ownerOrder = await clientB.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        ownerOrder.StatusCode.ShouldBe(HttpStatusCode.OK, await ownerOrder.Content.ReadAsStringAsync());
        using var ownerTickets = await clientB.GetAsync(
            $"{TestConstants.ShopBasePath}/after-sales?storeId={storeB}&orderId={orderId}");
        ownerTickets.StatusCode.ShouldBe(HttpStatusCode.OK, await ownerTickets.Content.ReadAsStringAsync());
        (await ownerTickets.DeserializeAsync<IReadOnlyList<ShopAfterSalesTicketDto>>())
            .ShouldHaveSingleItem().Id.ShouldBe(ticketId);

        using var foreignOrder = await clientA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderId}");
        foreignOrder.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignOrder.Content.ReadAsStringAsync());
        using var foreignCancel = await clientA.PostAsync(
            $"{TestConstants.ShopBasePath}/orders/{orderId}/cancel", content: null);
        foreignCancel.StatusCode.ShouldBe(HttpStatusCode.Conflict, await foreignCancel.Content.ReadAsStringAsync());
        (await foreignCancel.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");

        using var foreignTickets = await clientA.GetAsync(
            $"{TestConstants.ShopBasePath}/after-sales?orderId={orderId}");
        foreignTickets.StatusCode.ShouldBe(HttpStatusCode.OK, await foreignTickets.Content.ReadAsStringAsync());
        (await foreignTickets.DeserializeAsync<IReadOnlyList<ShopAfterSalesTicketDto>>()).ShouldBeEmpty();
        using var foreignCreate = await clientA.PostAsJsonAsync(
            $"{TestConstants.ShopBasePath}/after-sales",
            new { orderId, orderLineId, type = "Return", quantity = 1m, reason = "foreign order" });
        foreignCreate.StatusCode.ShouldBe(HttpStatusCode.NotFound, await foreignCreate.Content.ReadAsStringAsync());
    }

    private async Task<(Guid OrderId, Guid OrderLineId, Guid TicketId)> SeedOrderAndAfterSalesAsync(
        string customerTenantId,
        Guid customerOrgId,
        Guid storeId,
        Guid warehouseId,
        Guid productId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var order = SalesOrder.CreateDraft(
            $"ISO{Guid.NewGuid():N}"[..32],
            storeId,
            customerOrgId,
            warehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTimeOffset.UtcNow.AddHours(1),
            [(productId, "Ambient", 2m, 9.5m, "USD")],
            customerTenantId);
        var ticket = AfterSalesTicket.Create(
            order.Id,
            storeId,
            order.Lines[0].Id,
            AfterSalesTicketType.Return,
            1m,
            "seeded ticket",
            Guid.NewGuid(),
            customerTenantId);
        db.SalesOrders.Add(order);
        db.AfterSalesTickets.Add(ticket);
        await db.SaveChangesAsync();
        return (order.Id, order.Lines[0].Id, ticket.Id);
    }
}
