using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Data;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Integration.Tests.Tests.Ordering;

public sealed partial class CustomerShopIsolationTests
{
    private async Task AssertReconciliationBoundaryAsync(
        HttpClient root, HttpClient customerA, HttpClient customerB, HttpClient otherStoreEmployee,
        string tenantA, string tenantB, Guid warehouseId, Guid storeA, Guid storeB, Guid orderA, Guid orderB)
    {
        using var finance = await OperatorTestUsers.CreateOperatorAsync(_factory,
            OrderingPermissions.Orders.View, OrderingPermissions.Orders.Reconcile);
        using var viewer = await OperatorTestUsers.CreateOperatorAsync(_factory, OrderingPermissions.Orders.View);
        var reconcilePath = $"{TestConstants.OrderingBasePath}/orders/{orderA}/reconcile";

        foreach (var denied in new[] { customerA, customerB, otherStoreEmployee, viewer })
        {
            using var response = await denied.PostAsJsonAsync(reconcilePath, new { });
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        using var notReceived = await finance.PostAsJsonAsync(
            $"{TestConstants.OrderingBasePath}/orders/{orderB}/reconcile", new { });
        notReceived.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var beforeResponse = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderA}");
        var before = await beforeResponse.DeserializeAsync<ShopOrderDto>();
        before.Status.ShouldBe("Received");
        using var pending = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=received&storeId={storeA}");
        pending.EnsureSuccessStatusCode();
        (await pending.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldHaveSingleItem().Id.ShouldBe(orderA);
        using var financePending = await finance.GetAsync($"{TestConstants.OrderingBasePath}/orders?status=Received&storeId={storeA}");
        financePending.EnsureSuccessStatusCode();
        (await financePending.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items.ShouldHaveSingleItem().Id.ShouldBe(orderA);

        using var balancesBefore = await root.GetAsync($"{TestConstants.InventoryBasePath}/stock/balances?warehouseId={warehouseId}");
        balancesBefore.EnsureSuccessStatusCode();
        var stockSnapshot = await balancesBefore.Content.ReadAsStringAsync();
        // Different requests exercise the domain's repeat protection, not just HTTP response caching.
        for (int i = 0; i < 2; i++)
        {
            using var response = await finance.PostAsJsonAsync(reconcilePath, new { });
            response.EnsureSuccessStatusCode();
            (await response.DeserializeAsync<Guid>()).ShouldBe(orderA);
        }
        using var balancesAfter = await root.GetAsync($"{TestConstants.InventoryBasePath}/stock/balances?warehouseId={warehouseId}");
        balancesAfter.EnsureSuccessStatusCode();
        (await balancesAfter.Content.ReadAsStringAsync()).ShouldBe(stockSnapshot);

        using var closed = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled&storeId={storeA}");
        closed.EnsureSuccessStatusCode();
        var after = (await closed.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldHaveSingleItem();
        after.Id.ShouldBe(orderA);
        after.Status.ShouldBe("Reconciled");
        after.Lines.ShouldBe(before.Lines);
        using var closedJson = JsonDocument.Parse(await closed.Content.ReadAsStringAsync());
        AssertOrderPayload(closedJson.RootElement.GetProperty("items")[0].GetRawText());
        using var financeClosed = await finance.GetAsync($"{TestConstants.OrderingBasePath}/orders?status=Reconciled&storeId={storeA}");
        financeClosed.EnsureSuccessStatusCode();
        var operational = (await financeClosed.DeserializeAsync<PagedResult<SalesOrderDto>>()).Items.ShouldHaveSingleItem();
        operational.Id.ShouldBe(after.Id);
        operational.Lines.Single().UnitPrice.ShouldBe(after.Lines.Single().UnitPrice);
        operational.Lines.Single().Currency.ShouldBe(after.Lines.Single().Currency);
        operational.Lines.Single().DeliveredQty.ShouldBe(after.Lines.Single().DeliveredQty);
        using var noPending = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Received");
        (await noPending.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldBeEmpty();

        foreach (var excluded in new[] { customerB, otherStoreEmployee })
        {
            using var list = await excluded.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled");
            list.EnsureSuccessStatusCode();
            (await list.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldBeEmpty();
            using var detail = await excluded.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderA}");
            detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using var foreignStore = await excluded.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled&storeId={storeA}");
            foreignStore.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        using var remaining = await customerB.GetAsync($"{TestConstants.ShopBasePath}/orders?status=InTransit&storeId={storeB}");
        (await remaining.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldHaveSingleItem().Id.ShouldBe(orderB);
        foreach (var invalid in new[] { "Paid", "999", "7", "Received,Reconciled" })
        {
            foreach (var (client, prefix) in new[] { (finance, TestConstants.OrderingBasePath), (customerA, TestConstants.ShopBasePath) })
            {
                using var response = await client.GetAsync($"{prefix}/orders?status={Uri.EscapeDataString(invalid)}");
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }
        }

        var userId = await WaveAssignments.UserIdAsync(customerA);
        await SetDeliveryStoreAccessAsync(customerA, userId);
        using var revokedList = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled");
        revokedList.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var revokedDetail = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderA}");
        revokedDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await SetDeliveryStoreAccessAsync(customerA, userId, storeA);

        Guid originalOrg = Guid.Empty;
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            var order = await db.SalesOrders.SingleAsync(item => item.Id == orderA);
            originalOrg = order.CustomerOrgId;
            db.Entry(order).Property(item => item.CustomerOrgId).CurrentValue =
                (await db.Stores.SingleAsync(item => item.Id == storeB)).CustomerOrgId;
            await db.SaveChangesAsync();
        });
        using var wrongOrgList = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled");
        wrongOrgList.EnsureSuccessStatusCode();
        (await wrongOrgList.DeserializeAsync<PagedResult<ShopOrderDto>>()).Items.ShouldBeEmpty();
        using var wrongOrgDetail = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderA}");
        wrongOrgDetail.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            var order = await db.SalesOrders.SingleAsync(item => item.Id == orderA);
            db.Entry(order).Property(item => item.CustomerOrgId).CurrentValue = originalOrg;
            await db.SaveChangesAsync();
        });

        // Simulate a historical ownership mismatch in the isolated database, retaining the old grant.
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            var store = await db.Stores.SingleAsync(item => item.Id == storeA);
            store.AssignCustomerTenant(tenantB);
            await db.SaveChangesAsync();
        });
        using var staleGrant = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders?status=Reconciled");
        staleGrant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var staleDetail = await customerA.GetAsync($"{TestConstants.ShopBasePath}/orders/{orderA}");
        staleDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await InDeliveryScopeAsync("root", async services =>
        {
            var db = services.GetRequiredService<OrderingDbContext>();
            var store = await db.Stores.SingleAsync(item => item.Id == storeA);
            store.AssignCustomerTenant(tenantA);
            await db.SaveChangesAsync();
        });
    }
}
