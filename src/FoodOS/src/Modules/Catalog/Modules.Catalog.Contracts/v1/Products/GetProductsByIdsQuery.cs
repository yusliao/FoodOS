using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

public sealed record GetProductsByIdsQuery(IReadOnlyList<Guid> ProductIds)
    : IQuery<IReadOnlyList<ProductDto>>;
