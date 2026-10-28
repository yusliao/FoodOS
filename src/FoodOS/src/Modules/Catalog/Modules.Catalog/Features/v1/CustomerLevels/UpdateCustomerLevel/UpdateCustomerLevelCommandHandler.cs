using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.UpdateCustomerLevel;

public sealed class UpdateCustomerLevelCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<UpdateCustomerLevelCommand, Unit>
{
    public async ValueTask<Unit> Handle(UpdateCustomerLevelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var level = await dbContext.CustomerLevels
            .SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Customer level {command.Id} not found.");

        string code = CustomerLevel.NormalizeCode(command.Code);
        if (code != level.Code)
        {
            bool taken = await dbContext.CustomerLevels
                .AnyAsync(item => item.Code == code && item.Id != level.Id, cancellationToken)
                .ConfigureAwait(false);
            if (taken)
            {
                throw CustomerLevelErrors.CodeTaken(code);
            }
        }

        level.Update(code, command.Name, command.DiscountPercent);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw CustomerLevelErrors.CodeTaken(code);
        }

        return Unit.Value;
    }
}
