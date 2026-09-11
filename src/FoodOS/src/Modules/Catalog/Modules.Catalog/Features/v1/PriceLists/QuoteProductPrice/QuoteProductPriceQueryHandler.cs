using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.QuoteProductPrice;

public sealed class QuoteProductPriceQueryHandler(CatalogDbContext dbContext, TimeProvider clock)
    : IQueryHandler<QuoteProductPriceQuery, PriceQuoteDto>
{
    public async ValueTask<PriceQuoteDto> Handle(QuoteProductPriceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ProductId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Product {query.ProductId} not found.");

        DateTimeOffset asOf = query.AsOf ?? clock.GetUtcNow();

        var priceLock = await dbContext.ProductContractLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.CustomerOrgId == query.CustomerOrgId && l.ProductId == query.ProductId,
                cancellationToken)
            .ConfigureAwait(false);

        var lists = await dbContext.PriceLists
            .AsNoTracking()
            .Where(l => l.CustomerOrgId == query.CustomerOrgId || l.CustomerOrgId == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var (unitPrice, currency, source) = PriceResolver.Resolve(
            query.CustomerOrgId,
            query.ProductId,
            product.Price,
            query.Quantity,
            asOf,
            priceLock,
            lists);

        return new PriceQuoteDto(
            query.CustomerOrgId,
            query.ProductId,
            query.Quantity,
            unitPrice,
            currency,
            source);
    }
}
