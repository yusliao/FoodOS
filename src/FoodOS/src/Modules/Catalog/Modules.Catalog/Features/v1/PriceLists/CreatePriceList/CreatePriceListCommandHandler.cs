using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.CreatePriceList;

public sealed class CreatePriceListCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<CreatePriceListCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreatePriceListCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CustomerLevelId is { } levelId && levelId != Guid.Empty)
        {
            bool levelExists = await dbContext.CustomerLevels
                .AnyAsync(level => level.Id == levelId, cancellationToken)
                .ConfigureAwait(false);
            if (!levelExists)
            {
                throw new NotFoundException($"Customer level {levelId} not found.");
            }
        }

        var list = PriceList.Create(
            command.Name,
            command.CustomerOrgId,
            command.ValidFrom,
            command.ValidTo,
            command.Priority,
            command.CustomerLevelId);

        IReadOnlyList<PriceListLineInput> lines = command.Lines ?? [];
        if (lines.Count > 0)
        {
            var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
            int found = await dbContext.Products
                .CountAsync(p => productIds.Contains(p.Id), cancellationToken)
                .ConfigureAwait(false);
            if (found != productIds.Count)
            {
                throw new NotFoundException("One or more products were not found.");
            }

            await PriceListConflicts.EnsureNoSamePriorityConflictAsync(dbContext, list, productIds, cancellationToken)
                .ConfigureAwait(false);

            foreach (var line in lines)
            {
                list.UpsertLine(line.ProductId, line.MinQty, line.UnitPrice, line.Currency);
            }
        }

        dbContext.PriceLists.Add(list);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return list.Id;
    }
}
