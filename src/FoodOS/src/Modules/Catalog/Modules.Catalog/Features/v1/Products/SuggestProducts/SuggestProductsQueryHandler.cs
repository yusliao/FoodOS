using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products.SuggestProducts;

public sealed class SuggestProductsQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<SuggestProductsQuery, IReadOnlyList<ProductDto>>
{
    public async ValueTask<IReadOnlyList<ProductDto>> Handle(SuggestProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var words = ProductSearch.Words(query.Term);
        if (words.Length == 0)
        {
            return [];
        }

        var candidates = ProductSearch.Match(
            dbContext.Products.AsNoTracking().Where(product => product.IsActive),
            words);
        var products = await ProductSearch.OrderByRelevance(candidates, words)
            .Take(Math.Clamp(query.Limit, 1, 20))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return products.Select(product => product.ToDto()).ToList();
    }
}
