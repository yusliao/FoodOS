using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Categories;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Categories.GetActiveProductCategories;

public sealed class GetActiveProductCategoriesQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetActiveProductCategoriesQuery, IReadOnlyList<ProductCategoryCountDto>>
{
    public async ValueTask<IReadOnlyList<ProductCategoryCountDto>> Handle(
        GetActiveProductCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        // 分类数量很少：先按分类统计上架商品数，再取名称
        var counts = await dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken)
            .ConfigureAwait(false);
        if (counts.Count == 0)
        {
            return [];
        }

        var categoryIds = counts.Keys.ToList();
        var categories = await dbContext.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return categories
            .Select(category => new ProductCategoryCountDto(category.Id, category.Name, counts[category.Id]))
            .OrderBy(item => item.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
