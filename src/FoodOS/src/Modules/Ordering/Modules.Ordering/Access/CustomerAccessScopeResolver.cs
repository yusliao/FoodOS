using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Access;

public sealed class CustomerAccessScopeResolver(
    OrderingDbContext dbContext,
    ICurrentUser currentUser) : ICustomerAccessScopeResolver
{
    public Task<CustomerAccessScope> ResolveCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetTenant();
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("A restaurant customer identity is required.");
        }

        return ResolveAsync(tenantId, currentUser.GetUserId(), cancellationToken);
    }

    public async Task<CustomerAccessScope> ResolveAsync(
        string customerTenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTenantId);
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        var normalizedTenantId = customerTenantId.Trim().ToUpperInvariant();
        var org = await dbContext.CustomerOrgs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CustomerTenantId == normalizedTenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException("The customer tenant is not linked to a customer organization.");

        var storeIds = await dbContext.CustomerUserStoreAccesses
            .AsNoTracking()
            .Where(access =>
                access.CustomerTenantId == normalizedTenantId
                && access.CustomerOrgId == org.Id
                && access.UserId == userId
                && access.IsActive)
            .Select(access => access.StoreId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (storeIds.Count == 0)
        {
            throw new ForbiddenException("The customer user has no authorized store.");
        }

        return new CustomerAccessScope(normalizedTenantId, org.Id, storeIds);
    }

    public async Task EnsureCurrentUserCanAccessStoreAsync(
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        if (storeId == Guid.Empty) throw new ArgumentException("StoreId is required.", nameof(storeId));

        var scope = await ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!scope.StoreIds.Contains(storeId))
        {
            throw new ForbiddenException("The requested store is outside the current user's access scope.");
        }
    }
}
