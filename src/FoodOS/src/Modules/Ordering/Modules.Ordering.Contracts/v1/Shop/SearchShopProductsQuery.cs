using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Shop;

public sealed record SearchShopProductsQuery(
    Guid? StoreId = null,
    string? Search = null,
    Guid? BrandId = null,
    Guid? CategoryId = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<ShopProductDto>>;

public sealed record GetShopProductByIdQuery(
    Guid ProductId,
    Guid? StoreId = null,
    decimal Quantity = 1m) : IQuery<ShopProductDto>;
