using FSH.Framework.Caching;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Identity.Claims;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Domain;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Identity.Authorization;

/// <summary>
/// Reconciles permission claims on the built-in roles (<see cref="RoleConstants.Admin"/>,
/// <see cref="RoleConstants.Basic"/>) for the current Finbuckle tenant context. For restaurant
/// tenants it also removes operator-only claims from every role, closing stale grants left by
/// older releases. The operation is idempotent and safe to run on every startup.
/// </summary>
public sealed class RolePermissionSyncer(
    IdentityDbContext context,
    RoleManager<FshRole> roleManager,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    HybridCache cache,
    TimeProvider timeProvider,
    ILogger<RolePermissionSyncer> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        bool isRoot = tenantId == MultitenancyConstants.Root.Id;

        int basicAdded = await SyncRoleAsync(RoleConstants.Basic, PermissionConstants.Basic, cancellationToken).ConfigureAwait(false);

        // The root Admin is the operator role. A restaurant tenant Admin receives only the
        // explicitly customer-facing catalog, never procurement/warehouse/platform permissions.
        var adminPermissions = isRoot
            ? PermissionConstants.Admin.Concat(PermissionConstants.Root).Distinct().ToList()
            : PermissionConstants.CustomerAdmin.ToList();
        int adminAdded = await SyncRoleAsync(RoleConstants.Admin, adminPermissions, cancellationToken).ConfigureAwait(false);

        int removed = isRoot
            ? 0
            : await RemoveUnavailableTenantPermissionsAsync(cancellationToken).ConfigureAwait(false);

        // If we wrote anything, drop the per-user permission cache so already-logged-in
        // sessions see the new perms on their next request rather than waiting for TTL.
        if (basicAdded + adminAdded + removed > 0)
        {
            await cache.RemoveByTagAsync(CacheKeys.Tags.Permissions, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> RemoveUnavailableTenantPermissionsAsync(CancellationToken cancellationToken)
    {
        var allowed = PermissionConstants.CustomerAdmin
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var claims = await context.RoleClaims
            .Where(rc => rc.ClaimType == ClaimConstants.Permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var toRemove = claims
            .Where(rc => rc.ClaimValue is not null && !allowed.Contains(rc.ClaimValue))
            .ToList();

        if (toRemove.Count == 0)
        {
            return 0;
        }

        context.RoleClaims.RemoveRange(toRemove);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Removed {Count} operator-only permission claim(s) from restaurant tenant '{Tenant}'",
                toRemove.Count,
                tenantAccessor.MultiTenantContext.TenantInfo?.Id);
        }
        return toRemove.Count;
    }

    private async Task<int> SyncRoleAsync(string roleName, IReadOnlyList<FshPermission> targetPermissions, CancellationToken cancellationToken)
    {
        var role = await roleManager.Roles
            .SingleOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            // Role not yet seeded — full IdentityDbInitializer.SeedAsync will create it the first time.
            return 0;
        }

        var existing = await context.RoleClaims
            .Where(rc => rc.RoleId == role.Id && rc.ClaimType == ClaimConstants.Permission)
            .Select(rc => rc.ClaimValue!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var toAdd = targetPermissions
            .Where(p => !existingSet.Contains(p.Name))
            .Select(p => new FshRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = p.Name,
                CreatedBy = "RolePermissionSyncer",
                CreatedOn = timeProvider.GetUtcNow(),
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return 0;
        }

        await context.RoleClaims.AddRangeAsync(toAdd, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Synced {Count} new permission claim(s) to '{Role}' for tenant '{Tenant}'",
                toAdd.Count,
                roleName,
                tenantAccessor.MultiTenantContext.TenantInfo?.Id);
        }

        return toAdd.Count;
    }
}
