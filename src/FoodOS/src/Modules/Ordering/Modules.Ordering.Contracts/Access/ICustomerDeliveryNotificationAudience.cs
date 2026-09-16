using FSH.Modules.Ordering.Contracts.Events;

namespace FSH.Modules.Ordering.Contracts.Access;

public interface ICustomerDeliveryNotificationAudience
{
    Task<IReadOnlyList<CustomerOrderNotificationTarget>> GetOrdersAsync(
        IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetRecipientUserIdsAsync(Guid orderId, Guid storeId,
        CustomerDeliveryActivity activity, CancellationToken cancellationToken);
}

public sealed record CustomerOrderNotificationTarget(Guid OrderId, Guid StoreId, string CustomerTenantId);
