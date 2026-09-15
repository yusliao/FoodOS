using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.QuoteProductPrices;

public sealed class QuoteProductPricesQueryHandler(CatalogDbContext dbContext, TimeProvider clock)
    : IQueryHandler<QuoteProductPricesQuery, IReadOnlyList<PriceQuoteDto>>
{
    public async ValueTask<IReadOnlyList<PriceQuoteDto>> Handle(
        QuoteProductPricesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var requests = query.Products
            .GroupBy(item => item.ProductId)
            .Select(group => group.First())
            .ToList();
        if (requests.Count == 0)
        {
            return [];
        }

        var productIds = requests.Select(item => item.ProductId).ToList();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken)
            .ConfigureAwait(false);
        if (products.Count != productIds.Count)
        {
            var missing = productIds.First(id => !products.ContainsKey(id));
            throw new NotFoundException($"Product {missing} not found.");
        }

        DateTimeOffset asOf = query.AsOf ?? clock.GetUtcNow();
        var priceLocks = await dbContext.ProductContractLocks
            .AsNoTracking()
            .Where(item => item.CustomerOrgId == query.CustomerOrgId && productIds.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken)
            .ConfigureAwait(false);
        var priceLists = await dbContext.PriceLists
            .AsNoTracking()
            .Where(list => list.CustomerOrgId == query.CustomerOrgId || list.CustomerOrgId == null)
            .Include(list => list.Lines)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return requests.Select(request =>
        {
            var product = products[request.ProductId];
            priceLocks.TryGetValue(request.ProductId, out var priceLock);
            var (unitPrice, currency, source) = PriceResolver.Resolve(
                query.CustomerOrgId,
                request.ProductId,
                product.Price,
                request.Quantity,
                asOf,
                priceLock,
                priceLists);
            return new PriceQuoteDto(
                query.CustomerOrgId,
                request.ProductId,
                request.Quantity,
                unitPrice,
                currency,
                source);
        }).ToList();
    }
}
