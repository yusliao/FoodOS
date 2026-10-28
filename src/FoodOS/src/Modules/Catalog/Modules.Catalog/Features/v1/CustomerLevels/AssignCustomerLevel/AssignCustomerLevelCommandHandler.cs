using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.AssignCustomerLevel;

public sealed class AssignCustomerLevelCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<AssignCustomerLevelCommand, Unit>
{
    public async ValueTask<Unit> Handle(AssignCustomerLevelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var existing = await dbContext.CustomerLevelAssignments
            .SingleOrDefaultAsync(item => item.CustomerOrgId == command.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false);

        // 不选等级 = 回到普通价
        if (command.CustomerLevelId is not { } levelId || levelId == Guid.Empty)
        {
            if (existing is not null)
            {
                dbContext.CustomerLevelAssignments.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Unit.Value;
        }

        bool levelExists = await dbContext.CustomerLevels
            .AnyAsync(level => level.Id == levelId, cancellationToken)
            .ConfigureAwait(false);
        if (!levelExists)
        {
            throw new NotFoundException($"Customer level {levelId} not found.");
        }

        if (existing is null)
        {
            dbContext.CustomerLevelAssignments.Add(CustomerLevelAssignment.Create(command.CustomerOrgId, levelId));
        }
        else
        {
            existing.MoveTo(levelId);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
