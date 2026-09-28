using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Ordering.Contracts.Services;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.Modules.Ordering.Services;

public sealed class CustomerTemplateService(
    IServiceScopeFactory scopeFactory,
    IMultiTenantStore<AppTenantInfo> tenantStore,
    TimeProvider timeProvider) : ICustomerTemplateService
{
    public async Task EnsureAsync(string tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Customer template cannot be created for the root tenant.", nameof(tenantId));
        }

        var tenant = await tenantStore.GetAsync(tenantId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' was not found during customer template setup.");
        var tenantName = tenant.Name;
        if (string.IsNullOrWhiteSpace(tenantName) || tenantName.Length > 128)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' must have a name of at most 128 characters.");
        }
        var root = await tenantStore.GetAsync(MultitenancyConstants.Root.Id).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Root tenant was not found during customer template setup.");

        // Identity users belong to the customer tenant; business records belong to root.
        using var customerScope = scopeFactory.CreateScope();
        customerScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var users = await customerScope.ServiceProvider.GetRequiredService<IUserService>()
            .GetListAsync(cancellationToken).ConfigureAwait(false);
        var admin = users.SingleOrDefault(user =>
            string.Equals(user.Email, tenant.AdminEmail, StringComparison.OrdinalIgnoreCase));
        if (admin is null || !Guid.TryParse(admin.Id, out var adminId))
        {
            throw new InvalidOperationException($"Admin user for tenant '{tenantId}' was not seeded.");
        }

        using var operatorScope = scopeFactory.CreateScope();
        var contextSetter = operatorScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>();
        contextSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(root);
        try
        {
            var db = operatorScope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            var normalizedTenantId = tenant.Id.ToUpperInvariant();
            var org = await db.CustomerOrgs
                .SingleOrDefaultAsync(item => item.CustomerTenantId == normalizedTenantId, cancellationToken)
                .ConfigureAwait(false);
            if (org is null)
            {
                org = CustomerOrg.Create(CodeFor("C", tenant.Id), tenantName, customerTenantId: tenant.Id);
                db.CustomerOrgs.Add(org);
            }

            var store = await db.Stores
                .Where(item => item.CustomerOrgId == org.Id)
                .OrderBy(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (store is null)
            {
                store = Store.CreateInitial(org.Id, CodeFor("S", tenant.Id), tenantName, tenant.Id);
                db.Stores.Add(store);
            }

            var hasAccess = await db.CustomerUserStoreAccesses.AnyAsync(access =>
                access.CustomerTenantId == normalizedTenantId
                && access.UserId == adminId
                && access.StoreId == store.Id, cancellationToken).ConfigureAwait(false);
            if (!hasAccess)
            {
                db.CustomerUserStoreAccesses.Add(CustomerUserStoreAccess.Create(
                    tenant.Id, org.Id, store.Id, adminId, timeProvider.GetUtcNow()));
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            contextSetter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        }
    }

    private static string CodeFor(string prefix, string tenantId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.ToUpperInvariant()));
        return prefix + Convert.ToHexString(hash)[..15];
    }
}
