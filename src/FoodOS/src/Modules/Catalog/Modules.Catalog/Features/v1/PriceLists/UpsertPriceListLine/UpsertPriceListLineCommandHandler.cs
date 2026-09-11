using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertPriceListLine;

public sealed class UpsertPriceListLineCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<UpsertPriceListLineCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpsertPriceListLineCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var list = await dbContext.PriceLists
            .FirstOrDefaultAsync(l => l.Id == command.PriceListId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Price list {command.PriceListId} not found.");

        bool productExists = await dbContext.Products
            .AnyAsync(p => p.Id == command.ProductId, cancellationToken)
            .ConfigureAwait(false);
        if (!productExists)
        {
            throw new NotFoundException($"Product {command.ProductId} not found.");
        }

        var existing = list.Lines.FirstOrDefault(l =>
            l.ProductId == command.ProductId && l.MinQty == command.MinQty);
        if (existing is not null)
        {
            existing.Update(command.UnitPrice, command.Currency);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var line = PriceListLine.Create(
            list.Id,
            command.ProductId,
            command.MinQty,
            command.UnitPrice,
            command.Currency);
        dbContext.PriceListLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return line.Id;
    }
}
