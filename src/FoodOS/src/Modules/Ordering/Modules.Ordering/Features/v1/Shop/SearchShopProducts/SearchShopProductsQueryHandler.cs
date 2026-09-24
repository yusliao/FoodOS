using FSH.Framework.Shared.Persistence;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopProducts;

public sealed class SearchShopProductsQueryHandler(
    ICustomerAccessScopeResolver accessScopeResolver,
    OrderingDbContext dbContext,
    IWmsAvailabilityReader availabilityReader,
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
        string? warehouseCode = await ResolveWarehouseCodeAsync(query.StoreId, access, cancellationToken)
            .ConfigureAwait(false);
        var availability = await GetAvailabilityAsync(
                warehouseCode,
                products.Items.Select(product => (product.Sku, product.BaseUom, 1m)).ToList(),
                cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<ShopProductDto>
        {
            Items = products.Items.Select(product =>
            {
                var quote = quoteByProduct[product.Id];
                return ToShopDto(product, quote, availability[(Normalize(product.Sku), Normalize(product.BaseUom))]);
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
        string? warehouseCode = await ResolveWarehouseCodeAsync(query.StoreId, access, cancellationToken)
            .ConfigureAwait(false);
        var availability = await GetAvailabilityAsync(
                warehouseCode,
                [(product.Sku, product.BaseUom, query.Quantity)],
                cancellationToken)
            .ConfigureAwait(false);
        return ToShopDto(
            product,
            quote,
            availability[(Normalize(product.Sku), Normalize(product.BaseUom))]);
    }

    private static ShopProductDto ToShopDto(
        FSH.Modules.Catalog.Contracts.Dtos.ProductDto product,
        FSH.Modules.Catalog.Contracts.Dtos.PriceQuoteDto quote,
        bool isAvailable)
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
            IsAvailable: isAvailable);

    private async Task<string?> ResolveWarehouseCodeAsync(
        Guid? requestedStoreId,
        CustomerAccessScope access,
        CancellationToken cancellationToken)
    {
        Guid? storeId = requestedStoreId ?? access.StoreIds.OrderBy(id => id).Cast<Guid?>().FirstOrDefault();
        if (storeId is null)
        {
            return null;
        }

        var store = await dbContext.Stores.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == storeId
                && item.CustomerOrgId == access.CustomerOrgId
                && item.CustomerTenantId == access.CustomerTenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {storeId} not found.");
        var warehouse = await mediator.Send(new GetWarehouseByIdQuery(store.DefaultWarehouseId), cancellationToken)
            .ConfigureAwait(false);
        return warehouse.Code;
    }

    private async Task<IReadOnlyDictionary<(string Sku, string Uom), bool>> GetAvailabilityAsync(
        string? warehouseCode,
        IReadOnlyCollection<(string Sku, string Uom, decimal Quantity)> products,
        CancellationToken cancellationToken)
    {
        if (warehouseCode is null)
        {
            return products.ToDictionary(
                product => (Normalize(product.Sku), Normalize(product.Uom)),
                _ => false);
        }

        var result = await availabilityReader.GetAvailabilityAsync(
                warehouseCode,
                products.Select(product => new WmsAvailabilityRequest(
                    product.Sku,
                    product.Uom,
                    product.Quantity)).ToList(),
                cancellationToken)
            .ConfigureAwait(false);
        return result.ToDictionary(
            item => (Normalize(item.Sku), Normalize(item.Uom)),
            item => item.IsAvailable);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
