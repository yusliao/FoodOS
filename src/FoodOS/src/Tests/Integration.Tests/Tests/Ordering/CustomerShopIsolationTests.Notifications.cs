using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Data;
using FSH.Modules.Notifications.Contracts.v1.DTOs;
using FSH.Modules.Notifications.Data;
using FSH.Modules.Notifications.IntegrationEventHandlers;
using FSH.Modules.Ordering.Contracts.Events;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    [Fact]
    public async Task CustomerDelivery_Should_Recheck_Store_User_And_Tenant_And_Deduplicate_Concurrent_Delivery()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var tenant = $"notify-{Guid.NewGuid():N}";
        var email = $"admin-{tenant}@tenant.test";
        await CreateTenantAsync(root, tenant, email);
        await WaitForProvisioningAsync(root, tenant);
        using var admin = await CreateDashboardClientAsync(email, tenant);
        var warehouse = await CreateWarehouseAsync(root);
        var org = await CreateCustomerOrgAsync(root, tenant, Guid.NewGuid().ToString("N")[..16]);
        var store = await CreateStoreAsync(root, org, warehouse.Id, Guid.NewGuid().ToString("N"));
        var otherStore = await CreateStoreAsync(root, org, warehouse.Id, Guid.NewGuid().ToString("N"));
        await GrantSelfStoreAccessAsync(admin, store);
        var userId = await WaveAssignments.UserIdAsync(admin);
        var member = await CreateDeliveryMemberAsync(tenant, true);
        var withoutPermission = await CreateDeliveryMemberAsync(tenant, false);
        await SetDeliveryStoreAccessAsync(admin, member.Id, otherStore);
        await SetDeliveryStoreAccessAsync(admin, withoutPermission.Id, store);
        using var memberClient = await CreateDashboardClientAsync(member.Email, tenant);
        Guid orderId = Guid.Empty;
        await InDeliveryScopeAsync("root", async services =>
        {
            // Consumer fixture only. The separate mixed-shipment test exercises real fulfillment transitions.
            var order = SalesOrder.CreateDraft(Guid.NewGuid().ToString("N"), store, org, warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow.AddDays(1),
                [(Guid.NewGuid(), "Ambient", 1m, 1m, "USD")], tenant);
            var db = services.GetRequiredService<OrderingDbContext>();
            db.SalesOrders.Add(order);
            db.Entry(order).Property(x => x.Status).CurrentValue = SalesOrderStatus.InTransit;
            await db.SaveChangesAsync();
            orderId = order.Id;
        });
        var activity = new CustomerOrderDeliveryIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, tenant,
            "notification-test", "Logistics", orderId, store, CustomerDeliveryActivity.Departed);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DeliverCustomerNotificationAsync(tenant, activity)));
        var notification = (await ReadCustomerDeliveryNotificationsAsync(admin)).ShouldHaveSingleItem();
        notification.Type.ShouldBe("shop.order-departed");
        (await ReadCustomerDeliveryNotificationsAsync(memberClient)).ShouldBeEmpty("Other-store membership grants no access");
        await InDeliveryScopeAsync(tenant, async services =>
        {
            (await services.GetRequiredService<NotificationsDbContext>().Notifications
                .AnyAsync(n => n.UserId == withoutPermission.Id.ToString())).ShouldBeFalse();
        });
        using var foreignRead = await memberClient.PostAsJsonAsync($"/api/v1/notifications/{notification.Id}/read", new { });
        foreignRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid(), StoreId = otherStore });
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid(), OrderId = Guid.NewGuid() });
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid(), Activity = CustomerDeliveryActivity.Delivered });
        (await ReadCustomerDeliveryNotificationsAsync(admin)).Count.ShouldBe(1);

        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            var organization = await db.CustomerOrgs.SingleAsync(o => o.Id == org);
            organization.AssignCustomerTenant("wrong-owner");
            await db.SaveChangesAsync();
        });
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid() });
        (await ReadCustomerDeliveryNotificationsAsync(admin)).Count.ShouldBe(1);
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            (await db.CustomerOrgs.SingleAsync(o => o.Id == org)).AssignCustomerTenant(tenant);
            await db.SaveChangesAsync();
        });
        await Should.ThrowAsync<InvalidOperationException>(() => DeliverCustomerNotificationAsync("root", activity));
        await Should.ThrowAsync<InvalidOperationException>(() => DeliverCustomerNotificationAsync(tenant, activity with { Source = "Other" }));

        await SetDeliveryStoreAccessAsync(admin, userId);
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid() });
        (await ReadCustomerDeliveryNotificationsAsync(admin)).Count.ShouldBe(1);
        await SetDeliveryStoreAccessAsync(admin, userId, store);
        await InDeliveryScopeAsync(tenant, async services =>
        {
            var users = services.GetRequiredService<UserManager<FshUser>>();
            var user = (await users.FindByIdAsync(userId.ToString())).ShouldNotBeNull();
            user.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        });
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid() });
        await InDeliveryScopeAsync(tenant, async services =>
        {
            var users = services.GetRequiredService<UserManager<FshUser>>();
            var user = (await users.FindByIdAsync(userId.ToString())).ShouldNotBeNull();
            user.IsActive = true;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        });
        (await ReadCustomerDeliveryNotificationsAsync(admin)).Count.ShouldBe(1);

        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<TenantDbContext>();
            var identity = await db.TenantInfo.SingleAsync(t => t.Id == tenant);
            identity.Deactivate();
            await db.SaveChangesAsync();
        });
        await DeliverCustomerNotificationAsync(tenant, activity with { Id = Guid.NewGuid() });
        await InDeliveryScopeAsync(tenant, async services =>
        {
            (await services.GetRequiredService<NotificationsDbContext>().Notifications
                .CountAsync(n => n.UserId == userId.ToString())).ShouldBe(1);
        });
    }

    [Fact]
    public async Task CustomerDeliveryTenantResolution_Should_Preserve_Casing_And_Reject_Ambiguous_Or_Unsupported_Tenants()
    {
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<TenantDbContext>();
            var id = $"MiXeD_%{Guid.NewGuid():N}";
            var tenant = new FSH.Framework.Shared.Multitenancy.AppTenantInfo(id, "Notification tenant", null, "notification@test.invalid");
            db.TenantInfo.Add(tenant);
            await db.SaveChangesAsync();
            var resolver = services.GetRequiredService<ITenantService>();
            (await resolver.FindSharedCustomerTenantIdAsync(id.ToUpperInvariant())).ShouldBe(id);
            (await resolver.FindSharedCustomerTenantIdAsync("root")).ShouldBeNull();
            (await resolver.FindSharedCustomerTenantIdAsync($"missing-{Guid.NewGuid():N}")).ShouldBeNull();
            tenant.ConnectionString = "Host=unsupported";
            await db.SaveChangesAsync();
            (await resolver.FindSharedCustomerTenantIdAsync(id)).ShouldBeNull();
            tenant.ConnectionString = "";
            tenant.ValidUpto = DateTime.UtcNow.AddYears(-1);
            await db.SaveChangesAsync();
            (await resolver.FindSharedCustomerTenantIdAsync(id)).ShouldBeNull();
            tenant.ValidUpto = DateTime.UtcNow.AddMonths(1);
            db.TenantInfo.Add(new FSH.Framework.Shared.Multitenancy.AppTenantInfo(id.ToUpperInvariant(), "Ambiguous", null, "collision@test.invalid"));
            await db.SaveChangesAsync();
            (await resolver.FindSharedCustomerTenantIdAsync(id)).ShouldBeNull();
        });
    }

    private async Task InDeliveryScopeAsync(string tenantId, Func<IServiceProvider, Task> action)
    {
        using var tenantScope = _factory.Services.GetRequiredService<IEventTenantScope>().Begin(tenantId);
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    private Task DeliverCustomerNotificationAsync(string tenantId, CustomerOrderDeliveryIntegrationEvent activity) =>
        InDeliveryScopeAsync(tenantId, services => ActivatorUtilities
            .CreateInstance<CustomerOrderDeliveryNotificationHandler>(services).HandleAsync(activity));

    private async Task<(Guid Id, string Email)> CreateDeliveryMemberAsync(string tenantId, bool basic)
    {
        var email = $"notify-{Guid.NewGuid():N}@example.com";
        Guid id = Guid.Empty;
        await InDeliveryScopeAsync(tenantId, async services =>
        {
            var users = services.GetRequiredService<UserManager<FshUser>>();
            var user = new FshUser { UserName = email, Email = email, FirstName = "Delivery", LastName = "Member", EmailConfirmed = true, IsActive = true };
            (await users.CreateAsync(user, TestConstants.DefaultPassword)).Succeeded.ShouldBeTrue();
            if (basic) (await users.AddToRoleAsync(user, RoleConstants.Basic)).Succeeded.ShouldBeTrue();
            id = Guid.Parse(user.Id);
        });
        return (id, email);
    }

    private static async Task SetDeliveryStoreAccessAsync(HttpClient admin, Guid userId, params Guid[] storeIds)
    {
        using var response = await admin.PutAsJsonAsync($"{TestConstants.OrderingBasePath}/store-access/users/{userId}", new { userId, storeIds });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyList<NotificationDto>> ReadCustomerDeliveryNotificationsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/notifications/?pageSize=200");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<IReadOnlyList<NotificationDto>>();
    }

}
