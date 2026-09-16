using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

internal static class OperationalNotificationScope
{
    public static void EnsureRootTenant(
        string? eventTenantId,
        IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
        string eventName)
    {
        var ambient = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (!string.Equals(ambient, MultitenancyConstants.Root.Id, StringComparison.Ordinal)
            || !string.Equals(ambient, eventTenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Operational event {eventName} requires matching root context: ambient '{ambient ?? "(none)"}', event '{eventTenantId ?? "(none)"}'.");
        }
    }
}
