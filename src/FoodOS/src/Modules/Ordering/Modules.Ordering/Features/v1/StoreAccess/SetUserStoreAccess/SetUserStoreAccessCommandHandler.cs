using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.StoreAccess.SetUserStoreAccess;

public sealed class SetUserStoreAccessCommandHandler(
    OrderingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService,
    TimeProvider timeProvider) : ICommandHandler<SetUserStoreAccessCommand>
{
    public async ValueTask<Unit> Handle(
        SetUserStoreAccessCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var tenantId = currentUser.GetTenant();
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Store access is managed inside the restaurant tenant identity domain.");
        }

        _ = await userService.GetAsync(command.UserId.ToString(), cancellationToken).ConfigureAwait(false);

        var normalizedTenantId = tenantId.Trim().ToUpperInvariant();
        var org = await dbContext.CustomerOrgs
            .SingleOrDefaultAsync(x => x.CustomerTenantId == normalizedTenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException("The customer tenant is not linked to a customer organization.");

        var requestedStoreIds = command.StoreIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var validStoreIds = await dbContext.Stores
            .Where(store => store.CustomerOrgId == org.Id && requestedStoreIds.Contains(store.Id))
            .Select(store => store.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (validStoreIds.Count != requestedStoreIds.Count)
        {
            throw new ForbiddenException("One or more stores are outside the customer organization.");
        }

        var existing = await dbContext.CustomerUserStoreAccesses
            .Where(access => access.CustomerTenantId == normalizedTenantId && access.UserId == command.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var access in existing)
        {
            access.SetActive(requestedStoreIds.Contains(access.StoreId));
        }

        var existingStoreIds = existing.Select(access => access.StoreId).ToHashSet();
        foreach (var storeId in requestedStoreIds.Except(existingStoreIds))
        {
            dbContext.CustomerUserStoreAccesses.Add(CustomerUserStoreAccess.Create(
                normalizedTenantId,
                org.Id,
                storeId,
                command.UserId,
                timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
