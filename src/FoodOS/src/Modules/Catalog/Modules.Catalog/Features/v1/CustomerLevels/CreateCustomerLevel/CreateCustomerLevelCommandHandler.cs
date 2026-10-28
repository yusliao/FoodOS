using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.CreateCustomerLevel;

public sealed class CreateCustomerLevelCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<CreateCustomerLevelCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomerLevelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        string code = CustomerLevel.NormalizeCode(command.Code);
        bool taken = await dbContext.CustomerLevels
            .AnyAsync(level => level.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw CustomerLevelErrors.CodeTaken(code);
        }

        var level = CustomerLevel.Create(code, command.Name, command.DiscountPercent);
        dbContext.CustomerLevels.Add(level);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // 并发抢同一个编码时，唯一索引兜底
            throw CustomerLevelErrors.CodeTaken(code);
        }

        return level.Id;
    }
}
