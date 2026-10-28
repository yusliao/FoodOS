using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

public sealed class ShopCartOrdersHandler(
    OrderingDbContext dbContext,
    ICustomerAccessScopeResolver accessScopeResolver,
    IMediator mediator,
    PlatformOrderPlacement placement)
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
        var existingOrder = await placement
            .FindByIdempotencyKeyAsync(access.CustomerTenantId, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingOrder is not null)
        {
            if (existingOrder.StoreId != command.StoreId || existingOrder.CustomerOrgId != access.CustomerOrgId)
            {
                throw Conflict("The idempotency key was already used for another store order.");
            }

            return existingOrder.Id;
        }

        var store = await dbContext.Stores
            .FirstAsync(item => item.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false);
        var org = await placement.RequireOrderableAsync(store, cancellationToken).ConfigureAwait(false);
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

        var order = await placement.AddPlacedOrderAsync(
                store,
                org,
                cart.Lines.Select(line => (line.ProductId, line.Quantity)).ToList(),
                command.IdempotencyKey,
                SalesOrderPlacementChannel.Shop,
                placedByUserId: null,
                cancellationToken)
            .ConfigureAwait(false);
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

    private static CustomException Conflict(string message) => PlatformOrderPlacement.Conflict(message);
}
