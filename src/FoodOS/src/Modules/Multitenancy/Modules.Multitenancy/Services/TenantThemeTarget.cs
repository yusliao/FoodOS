using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts;

namespace FSH.Modules.Multitenancy.Services;

internal static class TenantThemeTarget
{
    internal static async Task<string> ResolveAsync(
        string? targetTenantId,
        IMultiTenantContextAccessor<AppTenantInfo> accessor,
        ICurrentUser user,
        ITenantService tenants,
        CancellationToken ct)
    {
        var identityTenant = user.GetTenant();
        var contextTenant = accessor.MultiTenantContext?.TenantInfo?.Id;
        if (!user.IsAuthenticated() || string.IsNullOrWhiteSpace(identityTenant)
            || !string.Equals(identityTenant, contextTenant, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Theme access requires a matching authenticated tenant identity.");
        }

        if (targetTenantId is null) return identityTenant;
        if (!string.Equals(identityTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(targetTenantId))
        {
            throw new ForbiddenException("Only the operator can address a tenant theme explicitly.");
        }

        // Resolve the resource, never change the caller's tenant context or JWT domain.
        await tenants.GetStatusAsync(targetTenantId, ct).ConfigureAwait(false);
        return targetTenantId;
    }
}
