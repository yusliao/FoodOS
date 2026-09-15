using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Events;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.IntegrationEventHandlers;

public sealed class AssignSingleStoreToRegisteredUserHandler(
    OrderingDbContext dbContext,
    TimeProvider timeProvider) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (string.IsNullOrWhiteSpace(@event.TenantId)
            || string.Equals(@event.TenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(@event.UserId, out var userId))
        {
            return;
        }

        var tenantId = @event.TenantId.Trim().ToUpperInvariant();
        var org = await dbContext.CustomerOrgs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerTenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (org is null)
        {
            return;
        }

        var storeIds = await dbContext.Stores
            .AsNoTracking()
            .Where(x => x.CustomerOrgId == org.Id)
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (storeIds.Count != 1)
        {
            return;
        }

        bool exists = await dbContext.CustomerUserStoreAccesses.AnyAsync(
            x => x.CustomerTenantId == tenantId && x.UserId == userId && x.StoreId == storeIds[0],
            ct).ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        dbContext.CustomerUserStoreAccesses.Add(CustomerUserStoreAccess.Create(
            tenantId,
            org.Id,
            storeIds[0],
            userId,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
