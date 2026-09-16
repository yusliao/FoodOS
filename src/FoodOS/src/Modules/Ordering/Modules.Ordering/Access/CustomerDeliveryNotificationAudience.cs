using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Events;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Access;

public sealed class CustomerDeliveryNotificationAudience(
    OrderingDbContext db, IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICustomerDeliveryNotificationAudience
{
    public async Task<IReadOnlyList<CustomerOrderNotificationTarget>> GetOrdersAsync(
        IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        if (tenantAccessor.MultiTenantContext.TenantInfo?.Id != MultitenancyConstants.Root.Id)
            throw new InvalidOperationException("Customer notification routing requires the operator scope.");

        return await ValidOrders().Where(order => orderIds.Contains(order.Id))
            .Select(order => new CustomerOrderNotificationTarget(order.Id, order.StoreId, order.CustomerTenantId!))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetRecipientUserIdsAsync(Guid orderId, Guid storeId,
        CustomerDeliveryActivity activity, CancellationToken cancellationToken)
    {
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase)
            || !Enum.IsDefined(activity)) return [];

        var normalized = tenantId.ToUpperInvariant();
        return await (from order in ValidOrders()
                      join access in db.CustomerUserStoreAccesses.AsNoTracking() on order.StoreId equals access.StoreId
                      where order.Id == orderId && order.StoreId == storeId
                          && order.CustomerTenantId == normalized
                          && access.CustomerTenantId == normalized && access.CustomerOrgId == order.CustomerOrgId
                          && access.IsActive
                          && (activity == CustomerDeliveryActivity.Departed
                              || order.Status == SalesOrderStatus.Received || order.Status == SalesOrderStatus.Reconciled)
                      select access.UserId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<SalesOrder> ValidOrders() => db.SalesOrders.AsNoTracking().Where(order =>
        order.CustomerTenantId != null
        && (order.Status == SalesOrderStatus.InTransit || order.Status == SalesOrderStatus.Received
            || order.Status == SalesOrderStatus.Reconciled)
        && db.CustomerOrgs.Any(org => org.Id == order.CustomerOrgId && org.CustomerTenantId == order.CustomerTenantId)
        && db.Stores.Any(store => store.Id == order.StoreId && store.CustomerOrgId == order.CustomerOrgId
            && store.CustomerTenantId == order.CustomerTenantId));
}
