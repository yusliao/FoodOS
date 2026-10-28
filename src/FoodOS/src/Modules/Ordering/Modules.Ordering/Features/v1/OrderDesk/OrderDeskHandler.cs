using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.OrderDesk;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1.Stores.StoreContacts;
using FSH.Modules.Ordering.Services;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.OrderDesk;

/// <summary>
/// Operator order desk: recognise the calling restaurant, look up products with the store's contract
/// prices, show its order history and place the order on its behalf.
/// </summary>
public sealed class OrderDeskHandler(
    OrderingDbContext dbContext,
    PlatformOrderPlacement placement,
    IWmsAvailabilityReader availabilityReader,
    ICurrentUser currentUser,
    IMediator mediator,
    TimeProvider clock)
    : IQueryHandler<SearchOrderDeskCallersQuery, IReadOnlyList<OrderDeskCallerDto>>,
      IQueryHandler<GetMyOrderDeskCallersQuery, IReadOnlyList<OrderDeskFrequentCallerDto>>,
      IQueryHandler<SuggestOrderDeskProductsQuery, IReadOnlyList<OrderDeskProductDto>>,
      IQueryHandler<QuoteOrderDeskLinesQuery, IReadOnlyList<OrderDeskQuoteLineDto>>,
      IQueryHandler<GetOrderDeskStoreHistoryQuery, OrderDeskStoreHistoryDto>,
      ICommandHandler<PlaceOrderDeskOrderCommand, Guid>
{
    private const string Escape = "\\";
    private const int HistoryDays = 90;
    private const int HistoryOrderScan = 500;

    public async ValueTask<IReadOnlyList<OrderDeskCallerDto>> Handle(
        SearchOrderDeskCallersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        string term = query.Term.Trim();
        string pattern = "%" + EscapeLike(term) + "%";
        string digits = PhoneNumbers.Digits(term);
        bool byPhone = digits.Length >= PhoneNumbers.MinSearchDigits;
        // Digits never contain LIKE wildcards, so they need no escaping.
        string digitsAnywhere = "%" + digits + "%";
        string digitsTail = "%" + digits;

        var matchingOrgIds = dbContext.CustomerOrgs
            .Where(org => EF.Functions.ILike(org.Name, pattern, Escape) || EF.Functions.ILike(org.Code, pattern, Escape))
            .Select(org => org.Id);
        var stores = await dbContext.Stores.AsNoTracking()
            .Include(store => store.Contacts)
            .Where(store =>
                EF.Functions.ILike(store.Name, pattern, Escape)
                || EF.Functions.ILike(store.Code, pattern, Escape)
                || matchingOrgIds.Contains(store.CustomerOrgId)
                || (byPhone && store.Contacts.Any(contact => EF.Functions.Like(contact.PhoneDigits, digitsAnywhere))))
            // A caller whose number matches a contact exactly (or by its national tail) comes first.
            .OrderByDescending(store => byPhone && store.Contacts.Any(contact =>
                EF.Functions.Like(contact.PhoneDigits, digitsTail) || EF.Functions.Like(digits, "%" + contact.PhoneDigits)))
            .ThenBy(store => store.Name)
            .ThenBy(store => store.Id)
            .Take(Math.Clamp(query.Limit, 1, 20))
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (stores.Count == 0)
        {
            return [];
        }

        return await ToCallersAsync(stores, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<OrderDeskFrequentCallerDto>> Handle(
        GetMyOrderDeskCallersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guid userId = currentUser.GetUserId();
        DateTimeOffset since = clock.GetUtcNow().AddDays(-HistoryDays);
        // 只统计这个客服自己通过客服下单渠道下的单
        var counts = await dbContext.SalesOrders.AsNoTracking()
            .Where(order => order.PlacedByUserId == userId
                && order.PlacementChannel == SalesOrderPlacementChannel.OrderDesk
                && order.CreatedAt >= since)
            .GroupBy(order => order.StoreId)
            .Select(group => new { StoreId = group.Key, Count = group.Count(), LastAt = group.Max(order => order.CreatedAt) })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.LastAt)
            .Take(Math.Clamp(query.Limit, 1, 20))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (counts.Count == 0)
        {
            return [];
        }

        var storeIds = counts.Select(item => item.StoreId).ToList();
        var stores = await dbContext.Stores.AsNoTracking()
            .Include(store => store.Contacts)
            .Where(store => storeIds.Contains(store.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var callers = (await ToCallersAsync(stores, cancellationToken).ConfigureAwait(false))
            .ToDictionary(caller => caller.StoreId);
        return counts
            .Where(item => callers.ContainsKey(item.StoreId))
            .Select(item => new OrderDeskFrequentCallerDto(callers[item.StoreId], item.Count))
            .ToList();
    }

    public async ValueTask<IReadOnlyList<OrderDeskProductDto>> Handle(
        SuggestOrderDeskProductsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var store = await RequireStoreAsync(query.StoreId, cancellationToken).ConfigureAwait(false);
        var products = await mediator.Send(new SuggestProductsQuery(query.Term, query.Limit), cancellationToken)
            .ConfigureAwait(false);
        if (products.Count == 0)
        {
            return [];
        }

        var quotes = await mediator.Send(
                new QuoteProductPricesQuery(
                    store.CustomerOrgId,
                    products.Select(product => new ProductPriceRequestDto(product.Id, 1m)).ToList()),
                cancellationToken)
            .ConfigureAwait(false);
        var quoteByProduct = quotes.ToDictionary(quote => quote.ProductId);
        var warehouse = await StoreWarehouseResolver.ResolveAsync(mediator, store.DefaultWarehouseId, cancellationToken)
            .ConfigureAwait(false);
        var availability = await ProductAvailability.GetAsync(
                availabilityReader,
                warehouse?.Code,
                products.Select(product => (product.Sku, product.BaseUom, 1m)).ToList(),
                cancellationToken)
            .ConfigureAwait(false);

        return products.Select(product =>
        {
            var quote = quoteByProduct[product.Id];
            var stock = ProductAvailability.For(availability, product.Sku, product.BaseUom);
            return new OrderDeskProductDto(
                product.Id,
                product.Sku,
                product.Name,
                product.BaseUom,
                product.CatchWeight,
                product.ThumbnailUrl,
                quote.UnitPrice,
                quote.Currency,
                quote.Source,
                stock.IsAvailable,
                stock.Status);
        }).ToList();
    }

    public async ValueTask<IReadOnlyList<OrderDeskQuoteLineDto>> Handle(
        QuoteOrderDeskLinesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var store = await RequireStoreAsync(query.StoreId, cancellationToken).ConfigureAwait(false);
        var quotes = await ShopCatalog.QuoteManyAsync(
                mediator,
                store.CustomerOrgId,
                query.Lines.Select(line => (line.ProductId, line.Quantity)),
                cancellationToken)
            .ConfigureAwait(false);
        return query.Lines.Select(line =>
        {
            var quote = quotes[line.ProductId];
            return new OrderDeskQuoteLineDto(
                line.ProductId,
                line.Quantity,
                quote.UnitPrice,
                quote.Currency,
                quote.Source,
                decimal.Round(quote.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero));
        }).ToList();
    }

    public async ValueTask<OrderDeskStoreHistoryDto> Handle(
        GetOrderDeskStoreHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var store = await RequireStoreAsync(query.StoreId, cancellationToken).ConfigureAwait(false);
        var recentOrders = await dbContext.SalesOrders.AsNoTracking()
            .Where(order => order.StoreId == store.Id)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(Math.Clamp(query.OrderLimit, 1, 50))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset since = clock.GetUtcNow().AddDays(-HistoryDays);
        var historyLines = await dbContext.SalesOrders.AsNoTracking()
            .Where(order => order.StoreId == store.Id
                && order.Status != SalesOrderStatus.Cancelled
                && order.CreatedAt >= since)
            .OrderByDescending(order => order.CreatedAt)
            .Take(HistoryOrderScan)
            .SelectMany(order => order.Lines.Select(line => new
            {
                OrderId = order.Id,
                order.CreatedAt,
                line.ProductId,
                line.OrderedQty,
            }))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var frequent = historyLines
            .GroupBy(line => line.ProductId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(line => line.CreatedAt).First();
                return new
                {
                    ProductId = group.Key,
                    OrderCount = group.Select(line => line.OrderId).Distinct().Count(),
                    TotalQty = group.Sum(line => line.OrderedQty),
                    LastQty = latest.OrderedQty,
                    LastOrderedAt = latest.CreatedAt,
                };
            })
            .OrderByDescending(item => item.OrderCount)
            .ThenByDescending(item => item.LastOrderedAt)
            .Take(Math.Clamp(query.FrequentLimit, 1, 50))
            .ToList();

        var productIds = recentOrders.SelectMany(order => order.Lines.Select(line => line.ProductId))
            .Concat(frequent.Select(item => item.ProductId))
            .Distinct()
            .ToList();
        var products = productIds.Count == 0
            ? new Dictionary<Guid, ProductDto>()
            : (await mediator.Send(new GetProductsByIdsQuery(productIds), cancellationToken).ConfigureAwait(false))
                .ToDictionary(product => product.Id);

        var orders = recentOrders.Select(order =>
        {
            var lines = order.Lines.Select(line =>
            {
                products.TryGetValue(line.ProductId, out var product);
                return new OrderDeskOrderLineDto(
                    line.Id,
                    line.ProductId,
                    product?.Sku ?? string.Empty,
                    product?.Name ?? string.Empty,
                    product?.BaseUom ?? string.Empty,
                    line.OrderedQty,
                    line.ShortageQty,
                    line.UnitPrice,
                    line.Currency);
            }).ToList();
            return new OrderDeskOrderDto(
                order.Id,
                order.Number,
                order.Status.ToString(),
                order.BusinessDate,
                order.PlacedAt,
                order.CutoffAt,
                order.Revision,
                order.PlacementChannel?.ToString(),
                order.WarehouseConfirmationStatus.ToString(),
                order.Lines.Sum(line => line.OrderedQty * line.UnitPrice),
                order.Lines.Select(line => line.Currency).FirstOrDefault() ?? "USD",
                lines);
        }).ToList();

        // Products that no longer exist are dropped; inactive ones stay visible but cannot be re-ordered.
        var frequentProducts = frequent
            .Where(item => products.ContainsKey(item.ProductId))
            .Select(item =>
            {
                var product = products[item.ProductId];
                return new OrderDeskFrequentProductDto(
                    item.ProductId,
                    product.Sku,
                    product.Name,
                    product.BaseUom,
                    product.IsActive,
                    item.OrderCount,
                    item.TotalQty,
                    item.LastQty,
                    item.LastOrderedAt);
            })
            .ToList();
        return new OrderDeskStoreHistoryDto(orders, frequentProducts);
    }

    public async ValueTask<Guid> Handle(PlaceOrderDeskOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var store = await dbContext.Stores
            .SingleOrDefaultAsync(item => item.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {command.StoreId} not found.");
        if (string.IsNullOrWhiteSpace(store.CustomerTenantId))
        {
            // The order must belong to the restaurant account so the customer can see and track it.
            throw PlatformOrderPlacement.Conflict("The store is not linked to a restaurant account.");
        }

        var existingOrder = await placement
            .FindByIdempotencyKeyAsync(store.CustomerTenantId, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingOrder is not null)
        {
            if (existingOrder.StoreId != store.Id)
            {
                throw PlatformOrderPlacement.Conflict("The idempotency key was already used for another store order.");
            }

            return existingOrder.Id;
        }

        var org = await placement.RequireOrderableAsync(store, cancellationToken).ConfigureAwait(false);
        Guid userId = currentUser.GetUserId();
        var order = await placement.AddPlacedOrderAsync(
                store,
                org,
                command.Lines.Select(line => (line.ProductId, line.Quantity)).ToList(),
                command.IdempotencyKey,
                SalesOrderPlacementChannel.OrderDesk,
                userId == Guid.Empty ? null : userId,
                cancellationToken)
            .ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }

    private async Task<Store> RequireStoreAsync(Guid storeId, CancellationToken cancellationToken) =>
        await dbContext.Stores.AsNoTracking()
            .SingleOrDefaultAsync(store => store.Id == storeId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new NotFoundException($"Store {storeId} not found.");

    private static string? BlockingReason(Store store, CustomerOrg org)
    {
        if (string.IsNullOrWhiteSpace(store.CustomerTenantId)) return "customerAccountMissing";
        if (string.IsNullOrWhiteSpace(store.Address)) return "addressMissing";
        return org.CreditHold ? "creditHold" : null;
    }

    private static string EscapeLike(string value) => value
        .Replace(Escape, Escape + Escape, StringComparison.Ordinal)
        .Replace("%", Escape + "%", StringComparison.Ordinal)
        .Replace("_", Escape + "_", StringComparison.Ordinal);

    /// <summary>Turns stores into caller cards: customer, last order, pricing level and whether orders can be placed.</summary>
    private async Task<List<OrderDeskCallerDto>> ToCallersAsync(List<Store> stores, CancellationToken cancellationToken)
    {
        var orgIds = stores.Select(store => store.CustomerOrgId).Distinct().ToList();
        var orgs = await dbContext.CustomerOrgs.AsNoTracking()
            .Where(org => orgIds.Contains(org.Id))
            .ToDictionaryAsync(org => org.Id, cancellationToken)
            .ConfigureAwait(false);
        var storeIds = stores.Select(store => store.Id).ToList();
        var lastOrders = await dbContext.SalesOrders.AsNoTracking()
            .Where(order => storeIds.Contains(order.StoreId))
            .GroupBy(order => order.StoreId)
            .Select(group => new { StoreId = group.Key, LastOrderAt = group.Max(order => order.CreatedAt) })
            .ToDictionaryAsync(item => item.StoreId, item => item.LastOrderAt, cancellationToken)
            .ConfigureAwait(false);
        // 等级归 Catalog 管，客服通常没有价目表权限，所以由后端一并带出
        var levels = (await mediator.Send(new GetCustomerLevelsByCustomersQuery(orgIds), cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => item.CustomerOrgId);

        return stores
            .Where(store => orgs.ContainsKey(store.CustomerOrgId))
            .Select(store =>
            {
                var org = orgs[store.CustomerOrgId];
                string? blocking = BlockingReason(store, org);
                return new OrderDeskCallerDto(
                    store.Id,
                    store.Code,
                    store.Name,
                    store.Address,
                    store.DeliveryWindow,
                    org.Id,
                    org.Code,
                    org.Name,
                    org.CreditHold,
                    CanPlaceOrders: blocking is null,
                    blocking,
                    StoreContactsHandler.ToDtos(store.Contacts),
                    lastOrders.TryGetValue(store.Id, out var lastOrderAt) ? lastOrderAt : null)
                {
                    LevelCode = levels.GetValueOrDefault(org.Id)?.Code,
                    LevelName = levels.GetValueOrDefault(org.Id)?.Name,
                    LevelDiscountPercent = levels.GetValueOrDefault(org.Id)?.DiscountPercent,
                };
            })
            .ToList();
    }
}
