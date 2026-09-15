using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;

public sealed class CreateCustomerOrgCommandHandler(OrderingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CreateCustomerOrgCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomerOrgCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customerTenantId = command.CustomerTenantId;
        if (string.IsNullOrWhiteSpace(customerTenantId)
            && !string.Equals(currentUser.GetTenant(), MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            customerTenantId = currentUser.GetTenant();
        }

        var org = CustomerOrg.Create(command.Code, command.Name, command.CreditHold, customerTenantId);
        if (org.CustomerTenantId is not null)
        {
            bool tenantTaken = await dbContext.CustomerOrgs
                .AnyAsync(o => o.CustomerTenantId == org.CustomerTenantId, cancellationToken)
                .ConfigureAwait(false);
            if (tenantTaken)
            {
                throw new CustomException(
                    $"Customer tenant '{org.CustomerTenantId}' is already linked to a customer organization.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.Conflict);
            }
        }
        bool taken = await dbContext.CustomerOrgs
            .AnyAsync(o => o.Code == org.Code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw new CustomException(
                $"A customer organization with code '{org.Code}' already exists.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        dbContext.CustomerOrgs.Add(org);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return org.Id;
    }
}
