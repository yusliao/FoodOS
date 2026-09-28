using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Ordering.Contracts.Services;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;
using Integration.Tests.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Ordering;

[Collection(FshCollectionDefinition.Name)]
public sealed class CustomerTemplateTests(FshWebApplicationFactory factory)
{
    [Fact]
    public async Task EnsureAsync_Creates_One_Editable_Store_And_Admin_Access_Without_Duplicates()
    {
        var tenantId = "template-" + Guid.NewGuid().ToString("N")[..8];
        using var setupScope = factory.Services.CreateScope();
        var tenantStore = setupScope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var root = await tenantStore.GetAsync(MultitenancyConstants.Root.Id);
        root.ShouldNotBeNull();

        var tenantService = setupScope.ServiceProvider.GetRequiredService<ITenantService>();
        await tenantService.CreateAsync(tenantId, "Template Restaurant", null,
            $"admin@{tenantId}.example", "test", "trial", DateTime.UtcNow.AddMonths(1), CancellationToken.None);
        var tenant = await tenantStore.GetAsync(tenantId);
        tenant.ShouldNotBeNull();
        await tenantService.MigrateTenantAsync(tenant, CancellationToken.None);
        await tenantService.SeedTenantAsync(tenant, CancellationToken.None);

        var template = setupScope.ServiceProvider.GetRequiredService<ICustomerTemplateService>();
        await template.EnsureAsync(tenantId, CancellationToken.None);
        await template.EnsureAsync(tenantId, CancellationToken.None);

        using var verifyScope = factory.Services.CreateScope();
        verifyScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(root);
        var ordering = verifyScope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var normalizedTenantId = tenantId.ToUpperInvariant();
        var org = await ordering.CustomerOrgs.SingleAsync(item => item.CustomerTenantId == normalizedTenantId);
        var store = await ordering.Stores.SingleAsync(item => item.CustomerOrgId == org.Id);
        store.DefaultWarehouseId.ShouldBe(Guid.Empty);
        store.Address.ShouldBe(string.Empty);
        (await ordering.CustomerUserStoreAccesses.CountAsync(item => item.StoreId == store.Id)).ShouldBe(1);

        var mediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();
        var placeOrder = new PlaceOrderCommandHandler(ordering, mediator, TimeProvider.System);
        var blocked = await Should.ThrowAsync<CustomException>(async () =>
            await placeOrder.Handle(new PlaceOrderCommand(store.Id), CancellationToken.None));
        blocked.Message.ShouldContain("delivery address");

        await mediator.Send(new UpdateCustomerOrgCommand(org.Id, "Updated Restaurant"), CancellationToken.None);
        await mediator.Send(new UpdateStoreCommand(store.Id, "Downtown", "1 Main St"), CancellationToken.None);
        await template.EnsureAsync(tenantId, CancellationToken.None);
        (await ordering.CustomerOrgs.SingleAsync(item => item.Id == org.Id)).Name.ShouldBe("Updated Restaurant");
        (await ordering.Stores.SingleAsync(item => item.Id == store.Id)).Address.ShouldBe("1 Main St");
    }
}
