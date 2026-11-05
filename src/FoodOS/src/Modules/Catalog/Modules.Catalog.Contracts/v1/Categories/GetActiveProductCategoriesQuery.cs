using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Categories;

/// <summary>Categories that hold at least one active product, with that count, ordered by name.</summary>
public sealed record GetActiveProductCategoriesQuery : IQuery<IReadOnlyList<ProductCategoryCountDto>>;
