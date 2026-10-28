using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
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
        if (command.Code is not null && CustomerOrg.NormalizeCode(command.Code) != org.Code)
        {
            string code = CustomerOrg.NormalizeCode(command.Code);
            bool taken = await dbContext.CustomerOrgs
                .AnyAsync(item => item.Code == code && item.Id != org.Id, cancellationToken)
                .ConfigureAwait(false);
            if (taken)
            {
                throw CodeTaken(code);
            }

            org.ChangeCode(code);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (command.Code is not null)
        {
            // A concurrent update claimed the same code; the unique index is the final guard.
            throw CodeTaken(CustomerOrg.NormalizeCode(command.Code));
        }

        return Unit.Value;
    }

    private static CustomException CodeTaken(string code) => new(
        $"A customer organization with code '{code}' already exists.",
        (IEnumerable<string>?)null,
        HttpStatusCode.Conflict);
}
