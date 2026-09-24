using System.Diagnostics;
using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

public sealed class ShopCartOrdersHandler(
    OrderingDbContext dbContext,
    ICustomerAccessScopeResolver accessScopeResolver,
    IWmsReservationGateway reservationGateway,
    IMediator mediator,
    TimeProvider clock)
    : IQueryHandler<GetShopCartQuery, ShopCartDto>,
      ICommandHandler<UpdateShopCartCommand, Guid>,
      IQueryHandler<SearchShopOrdersQuery, PagedResponse<ShopOrderDto>>,
      IQueryHandler<GetShopOrderByIdQuery, ShopOrderDto>,
      ICommandHandler<PlaceShopOrderCommand, Guid>,
      ICommandHandler<AmendShopOrderCommand, Guid>,
      ICommandHandler<CancelShopOrderCommand, Guid>
{
    public async ValueTask<ShopCartDto> Handle(GetShopCartQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await RequireStoreAsync(query.StoreId, cancellationToken).ConfigureAwait(false);
        var cart = await dbContext.Carts
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.StoreId == query.StoreId && item.CustomerTenantId == access.CustomerTenantId,
                cancellationToken)
            .ConfigureAwait(false);
        return cart?.ToShopDto() ?? new ShopCartDto(Guid.Empty, query.StoreId, [], DateTimeOffset.MinValue);
    }

    public async ValueTask<Guid> Handle(UpdateShopCartCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequireStoreAsync(command.StoreId, cancellationToken).ConfigureAwait(false);
        return await mediator.Send(new UpdateCartCommand(command.StoreId, command.Lines), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<PagedResponse<ShopOrderDto>> Handle(
        SearchShopOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (query.StoreId is { } storeId && !access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        IQueryable<SalesOrder> orders = dbContext.SalesOrders
            .AsNoTracking()
            .Where(order =>
                order.CustomerTenantId == access.CustomerTenantId
                && order.CustomerOrgId == access.CustomerOrgId
                && access.StoreIds.Contains(order.StoreId));
        if (query.StoreId is { } selectedStoreId)
        {
            orders = orders.Where(order => order.StoreId == selectedStoreId);
        }

        if (query.Status is not null)
        {
            var status = Enum.Parse<SalesOrderStatus>(query.Status, ignoreCase: true);
            orders = orders.Where(order => order.Status == status);
        }

        int page = query.PageNumber;
        int size = query.PageSize;
        long total = await orders.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResponse<ShopOrderDto>
        {
            Items = items.Select(order => order.ToShopDto()).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size),
        };
    }

    public async ValueTask<ShopOrderDto> Handle(GetShopOrderByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var order = await RequireOrderAsync(query.OrderId, cancellationToken).ConfigureAwait(false);
        return order.ToShopDto();
    }

    public async ValueTask<Guid> Handle(PlaceShopOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var access = await RequireStoreAsync(command.StoreId, cancellationToken).ConfigureAwait(false);
        var existingReservation = await reservationGateway.FindAsync(command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingReservation?.Status == "completed")
        {
            var existingOrder = await dbContext.SalesOrders.AsNoTracking()
                .SingleOrDefaultAsync(order =>
                    order.Id == existingReservation.OperationId
                    && order.CustomerTenantId == access.CustomerTenantId
                    && order.CustomerOrgId == access.CustomerOrgId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingOrder is not null)
            {
                if (existingOrder.StoreId != command.StoreId)
                {
                    throw Conflict("The idempotency key was already used for another store order.");
                }

                return existingOrder.Id;
            }
        }

        var store = await dbContext.Stores
            .FirstAsync(item => item.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false);
        var org = await dbContext.CustomerOrgs
            .FirstOrDefaultAsync(item => item.Id == store.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Customer organization {store.CustomerOrgId} not found.");
        if (org.CreditHold)
        {
            throw Conflict("Customer is on credit hold.");
        }

        var cart = await dbContext.Carts
            .FirstOrDefaultAsync(item => item.StoreId == store.Id, cancellationToken)
            .ConfigureAwait(false);
        if (cart is null || cart.Lines.Count == 0)
        {
            throw new CustomException(
                "Cart is empty.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var warehouse = await mediator.Send(new GetWarehouseByIdQuery(store.DefaultWarehouseId), cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset utcNow = clock.GetUtcNow();
        TimeOnly cutoffLocal = TimeOnly.ParseExact(
            warehouse.Clock.CutoffLocal,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var (businessDate, cutoffAt) = OperatingCutoff.Resolve(warehouse.Clock.TimeZoneId, cutoffLocal, utcNow);
        for (int i = 0; i < 7; i++)
        {
            var existingPlan = await mediator
                .Send(new GetDailyPlanQuery(warehouse.Id, businessDate), cancellationToken)
                .ConfigureAwait(false);
            if (existingPlan is null)
            {
                break;
            }

            (businessDate, cutoffAt) = OperatingCutoff.NextAfter(
                warehouse.Clock.TimeZoneId,
                cutoffLocal,
                businessDate);
        }

        var products = await ShopCatalog.GetActiveManyAsync(
                mediator,
                cart.Lines.Select(line => line.ProductId),
                cancellationToken)
            .ConfigureAwait(false);
        var quotes = await ShopCatalog.QuoteManyAsync(
                mediator,
                org.Id,
                cart.Lines.Select(line => (line.ProductId, line.Quantity)),
                cancellationToken)
            .ConfigureAwait(false);
        var draftLines = new List<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)>();
        var reservationLines = new List<WmsReservationLine>();
        foreach (var line in cart.Lines)
        {
            var (product, zone) = products[line.ProductId];
            var quote = quotes[line.ProductId];
            draftLines.Add((line.ProductId, zone.ToString(), line.Quantity, quote.UnitPrice, quote.Currency));
            reservationLines.Add(new(
                line.ProductId.ToString("N", CultureInfo.InvariantCulture),
                product.Sku,
                product.BaseUom,
                line.Quantity));
        }

        string correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var reservation = await reservationGateway.ReserveAsync(
                command.IdempotencyKey,
                correlationId,
                new WmsReserveOrderRequest(warehouse.Code, "root", reservationLines),
                cancellationToken)
            .ConfigureAwait(false);
        if (reservation.Status == "rejected")
        {
            throw Conflict($"WMS rejected the reservation ({reservation.ErrorCode ?? "rejected"}).");
        }

        if (reservation.Status != "completed")
        {
            throw Conflict(
                "WMS reservation is pending or its result is unknown. Retry with the same Idempotency-Key.");
        }

        var orderAlreadySaved = await dbContext.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == reservation.OperationId, cancellationToken)
            .ConfigureAwait(false);
        if (orderAlreadySaved is not null)
        {
            if (orderAlreadySaved.StoreId != command.StoreId
                || orderAlreadySaved.CustomerTenantId != access.CustomerTenantId)
            {
                throw Conflict("The idempotency key was already used for another store order.");
            }

            return orderAlreadySaved.Id;
        }

        string number = await OrderNumbers.NextAsync(dbContext, businessDate, cancellationToken).ConfigureAwait(false);
        var order = SalesOrder.CreateDraft(
            number,
            store.Id,
            org.Id,
            warehouse.Id,
            businessDate,
            cutoffAt,
            draftLines,
            store.CustomerTenantId,
            reservation.OperationId);
        foreach (var line in order.Lines)
        {
            line.BindReservation(reservation.OperationId, line.OrderedQty);
        }

        order.Place(utcNow);
        dbContext.SalesOrders.Add(order);
        dbContext.CartLines.RemoveRange(cart.Lines);
        cart.Clear();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }

    public async ValueTask<Guid> Handle(AmendShopOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequireOrderAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        return await mediator.Send(new AmendOrderCommand(command.OrderId, command.Lines), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<Guid> Handle(CancelShopOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequireOrderAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        return await mediator.Send(new CancelOrderCommand(command.OrderId), cancellationToken).ConfigureAwait(false);
    }

    private async Task<CustomerAccessScope> RequireStoreAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        bool exists = await dbContext.Stores.AsNoTracking().AnyAsync(store =>
                store.Id == storeId
                && store.CustomerOrgId == access.CustomerOrgId
                && store.CustomerTenantId == access.CustomerTenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        return access;
    }

    private async Task<SalesOrder> RequireOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.SalesOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(order =>
                order.Id == orderId
                && order.CustomerTenantId == access.CustomerTenantId
                && order.CustomerOrgId == access.CustomerOrgId
                && access.StoreIds.Contains(order.StoreId),
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {orderId} not found.");
    }

    private static CustomException Conflict(string message) => new(
        message,
        (IEnumerable<string>?)null,
        HttpStatusCode.Conflict);
}
