using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertProductPriceLock;

public sealed class UpsertProductPriceLockCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<UpsertProductPriceLockCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpsertProductPriceLockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool productExists = await dbContext.Products
            .AnyAsync(p => p.Id == command.ProductId, cancellationToken)
            .ConfigureAwait(false);
        if (!productExists)
        {
            throw new NotFoundException($"Product {command.ProductId} not found.");
        }

        var existing = await dbContext.ProductContractLocks
            .FirstOrDefaultAsync(
                l => l.CustomerOrgId == command.CustomerOrgId && l.ProductId == command.ProductId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Replace(command.UnitPrice, command.Currency, command.Until);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var priceLock = ProductContractLock.Create(
            command.CustomerOrgId,
            command.ProductId,
            command.UnitPrice,
            command.Currency,
            command.Until);
        dbContext.ProductContractLocks.Add(priceLock);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return priceLock.Id;
    }
}
