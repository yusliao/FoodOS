using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.UpdateCustomerOrg;

public sealed class UpdateCustomerOrgCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<UpdateCustomerOrgCommand>
{
    public async ValueTask<Unit> Handle(UpdateCustomerOrgCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var org = await dbContext.CustomerOrgs
            .SingleOrDefaultAsync(item => item.Id == command.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Customer organization {command.CustomerOrgId} not found.");
        org.Rename(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
