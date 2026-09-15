using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products.GetProductsByIds;

public sealed class GetProductsByIdsQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetProductsByIdsQuery, IReadOnlyList<ProductDto>>
{
    public async ValueTask<IReadOnlyList<ProductDto>> Handle(
        GetProductsByIdsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var productIds = query.ProductIds.Distinct().ToList();
        if (productIds.Count == 0)
        {
            return [];
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (products.Count != productIds.Count)
        {
            var foundIds = products.Select(product => product.Id).ToHashSet();
            var missing = productIds.First(id => !foundIds.Contains(id));
            throw new NotFoundException($"Product {missing} not found.");
        }

        var productById = products.ToDictionary(product => product.Id);
        return productIds.Select(id => productById[id].ToDto()).ToList();
    }
}
