using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Notifications.Contracts.Authorization;
using FSH.Modules.Notifications.Domain;
using FSH.Modules.Notifications.Features.v1.Internal;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Events;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class CustomerOrderDeliveryNotificationHandler(
    ICustomerDeliveryNotificationAudience audience, ITenantService tenants,
    IUserProfileService users, IUserPermissionService permissions, IdempotentInboxWriter inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IIntegrationEventHandler<CustomerOrderDeliveryIntegrationEvent>
{
    public async Task HandleAsync(CustomerOrderDeliveryIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId != @event.TenantId
            || string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase)
            || @event.Source != "Logistics" || @event.Id == Guid.Empty || !Enum.IsDefined(@event.Activity))
            throw new InvalidOperationException("Invalid customer order delivery scope.");
        if (await tenants.FindSharedCustomerTenantIdAsync(tenantId, ct).ConfigureAwait(false) != tenantId) return;

        var candidates = await audience.GetRecipientUserIdsAsync(@event.OrderId, @event.StoreId, @event.Activity, ct)
            .ConfigureAwait(false);
        var active = await users.GetActiveUserIdsAsync(candidates.Select(id => id.ToString()).ToArray(), ct).ConfigureAwait(false);
        var type = @event.Activity == CustomerDeliveryActivity.Departed ? "shop.order-departed" : "shop.order-delivered";
        foreach (var userId in active)
        {
            if (!await permissions.HasPermissionAsync(userId, OrderingPermissions.Shop.View, ct).ConfigureAwait(false)
                || !await permissions.HasPermissionAsync(userId, NotificationPermissions.Inbox.View, ct).ConfigureAwait(false)) continue;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"customer-order-notification:{@event.Id:N}:{userId}"));
            await inbox.WriteAsync(Notification.Create(userId, type,
                CustomerDeliveryNotificationText.Title(type, CultureInfo.GetCultureInfo("en-US"))!,
                body: null, link: $"/shop/orders/{@event.OrderId}", source: "Ordering",
                metadata: new { orderId = @event.OrderId, storeId = @event.StoreId, activity = type },
                notificationId: new Guid(hash.AsSpan(0, 16))), ct).ConfigureAwait(false);
        }
    }
}
