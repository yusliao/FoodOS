using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

/// <summary>
/// Type-ahead candidates among active products, ranked by relevance. The term matches names in every
/// language, SKU, barcode and pinyin (full or initials) of Chinese names.
/// </summary>
public sealed record SuggestProductsQuery(string Term, int Limit = 10) : IQuery<IReadOnlyList<ProductDto>>;
