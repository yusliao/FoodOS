using FSH.Framework.Shared.Persistence;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopProducts;

public sealed class SearchShopProductsQueryHandler(
    ICustomerAccessScopeResolver accessScopeResolver,
    IMediator mediator)
    : IQueryHandler<SearchShopProductsQuery, PagedResponse<ShopProductDto>>,
      IQueryHandler<GetShopProductByIdQuery, ShopProductDto>
{
    public async ValueTask<PagedResponse<ShopProductDto>> Handle(
        SearchShopProductsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (query.StoreId is { } storeId && !access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        var products = await mediator.Send(
                new SearchProductsQuery(
                    query.Search,
                    query.BrandId,
                    query.CategoryId,
                    IsActive: true,
                    query.PageNumber,
                    query.PageSize,
                    SortBy: "name",
                    SortDir: "asc"),
                cancellationToken)
            .ConfigureAwait(false);
        var quotes = await mediator.Send(
                new QuoteProductPricesQuery(
                    access.CustomerOrgId,
                    products.Items.Select(product => new ProductPriceRequestDto(product.Id, 1m)).ToList()),
                cancellationToken)
            .ConfigureAwait(false);
        var quoteByProduct = quotes.ToDictionary(quote => quote.ProductId);

        return new PagedResponse<ShopProductDto>
        {
            Items = products.Items.Select(product =>
            {
                var quote = quoteByProduct[product.Id];
                return ToShopDto(product, quote);
            }).ToList(),
            PageNumber = products.PageNumber,
            PageSize = products.PageSize,
            TotalCount = products.TotalCount,
            TotalPages = products.TotalPages,
        };
    }

    public async ValueTask<ShopProductDto> Handle(
        GetShopProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (query.StoreId is { } storeId && !access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        var product = await mediator.Send(new GetProductByIdQuery(query.ProductId), cancellationToken)
            .ConfigureAwait(false);
        if (!product.IsActive)
        {
            throw new NotFoundException($"Product {query.ProductId} not found.");
        }

        var quote = await mediator.Send(
                new QuoteProductPriceQuery(access.CustomerOrgId, query.ProductId, query.Quantity),
                cancellationToken)
            .ConfigureAwait(false);
        return ToShopDto(product, quote);
    }

    private static ShopProductDto ToShopDto(
        FSH.Modules.Catalog.Contracts.Dtos.ProductDto product,
        FSH.Modules.Catalog.Contracts.Dtos.PriceQuoteDto quote)
        => new(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.BrandId,
            product.CategoryId,
            quote.UnitPrice,
            quote.Currency,
            quote.Source,
            product.BaseUom,
            product.CatchWeight,
            product.ThumbnailUrl,
            IsAvailable: true);
}
