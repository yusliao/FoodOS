using FSH.Framework.Caching;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Modules.Identity.Caching;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;

namespace FSH.Modules.Identity.Services;

internal sealed class UserPermissionService(
    UserManager<FshUser> userManager,
    RoleManager<FshRole> roleManager,
    IdentityDbContext db,
    HybridCache cache,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor) : IUserPermissionService
{
    // Hoisted to avoid per-call allocations. Small payload (< 4 KB after base64), so compression
    // CPU beats the marginal network savings — disable it for this hot path.
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
        Flags = HybridCacheEntryFlags.DisableCompression,
    };

    private static readonly string[] Tags = [CacheKeys.Tags.Permissions];

    public async Task<List<string>?> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        var set = await GetOrLoadAsync(userId, cancellationToken).ConfigureAwait(false);

        // Copy to a new List<string> to preserve the public contract; ~50 ns is negligible vs the
        // JSON deserialization we'd otherwise pay per L1 hit without the [ImmutableObject] optimization.
        return [.. set.Values];
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        // Fast path: use the cached PermissionSet directly to avoid materializing a List<string>
        // just to check a single permission. Shares the cache entry with GetPermissionsAsync.
        var set = await GetOrLoadAsync(userId, cancellationToken).ConfigureAwait(false);
        return set.Contains(permission);
    }

    public Task InvalidatePermissionCacheAsync(string userId, CancellationToken cancellationToken)
        => cache.RemoveAsync(PermissionCacheKey(userId), cancellationToken).AsTask();

    private string PermissionCacheKey(string userId)
    {
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)) throw new UnauthorizedException("missing tenant context");
        return $"{CacheKeys.UserPermissions(userId)}:identity-v2:{tenantId}";
    }

    private ValueTask<PermissionSet> GetOrLoadAsync(string userId, CancellationToken cancellationToken)
    {
        // Stateless factory overload — the factory is a static method group, so the runtime
        // reuses a cached delegate and no closure is allocated per call (including L1 hits).
        var audience = tenantAccessor.MultiTenantContext.TenantInfo?.Id == MultitenancyConstants.Root.Id
            ? RoleAudiences.Operator : RoleAudiences.Customer;
        var state = new FactoryState(userManager, roleManager, db, userId, audience);

        return cache.GetOrCreateAsync(
            PermissionCacheKey(userId),
            state,
            LoadPermissionsAsync,
            options: EntryOptions,
            tags: Tags,
            cancellationToken: cancellationToken);
    }

    private static async ValueTask<PermissionSet> LoadPermissionsAsync(FactoryState s, CancellationToken ct)
    {
        var user = await s.UserManager.FindByIdAsync(s.UserId).ConfigureAwait(false);
        _ = user ?? throw new UnauthorizedException();

        var userRoles = await s.UserManager.GetRolesAsync(user).ConfigureAwait(false);

        var directRoleIds = await s.RoleManager.Roles
            .Where(r => userRoles.Contains(r.Name!) && r.Audience == s.Audience)
            .Select(r => r.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        // Group-derived roles confer permissions too — the JWT already unions them
        // (IdentityService.AddRoleClaimsAsync) and every group mutation invalidates this
        // cache entry, so the effective set must include roles reachable via UserGroups.
        var groupRoleIds = await s.Db.GroupRoles
            .Where(gr => s.Db.UserGroups
                .Where(ug => ug.UserId == s.UserId)
                .Select(ug => ug.GroupId)
                .Contains(gr.GroupId))
            .Select(gr => gr.RoleId)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var candidateRoleIds = directRoleIds.Union(groupRoleIds, StringComparer.Ordinal).ToList();
        var roleIds = await s.RoleManager.Roles.Where(role => candidateRoleIds.Contains(role.Id) && role.Audience == s.Audience)
            .Select(role => role.Id).ToListAsync(ct).ConfigureAwait(false);

        if (roleIds.Count == 0)
        {
            return PermissionSet.Empty;
        }

        // Single query across all role IDs — cheaper than the old N+1 loop.
        var perms = await s.Db.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == ClaimConstants.Permission)
            .Select(rc => rc.ClaimValue!)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        if (s.Audience == RoleAudiences.Customer)
        {
            var allowed = PermissionConstants.CustomerAdmin.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
            perms.RemoveAll(permission => !allowed.Contains(permission));
        }

        return perms.Count == 0
            ? PermissionSet.Empty
            : new PermissionSet([.. perms]);
    }

    // Struct state flows through HybridCache's TState parameter — avoids closure allocation.
    private readonly record struct FactoryState(
        UserManager<FshUser> UserManager,
        RoleManager<FshRole> RoleManager,
        IdentityDbContext Db,
        string UserId,
        string Audience);
}
