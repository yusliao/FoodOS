using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;

public sealed class CreateCustomerOrgCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<CreateCustomerOrgCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomerOrgCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var org = CustomerOrg.Create(command.Code, command.Name, command.CreditHold);
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
