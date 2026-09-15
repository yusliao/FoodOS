using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopAfterSales;

public sealed class ShopAfterSalesHandler(
    OrderingDbContext dbContext,
    ICustomerAccessScopeResolver accessScopeResolver,
    IMediator mediator)
    : IQueryHandler<SearchShopAfterSalesQuery, IReadOnlyList<ShopAfterSalesTicketDto>>,
      ICommandHandler<CreateShopAfterSalesCommand, ShopAfterSalesTicketDto>
{
    public async ValueTask<IReadOnlyList<ShopAfterSalesTicketDto>> Handle(
        SearchShopAfterSalesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (query.StoreId is { } storeId && !access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        var tickets = dbContext.AfterSalesTickets.AsNoTracking().Where(ticket =>
            ticket.CustomerTenantId == access.CustomerTenantId
            && access.StoreIds.Contains(ticket.StoreId));
        if (query.StoreId is { } selectedStoreId)
        {
            tickets = tickets.Where(ticket => ticket.StoreId == selectedStoreId);
        }
        if (query.OrderId is { } orderId)
        {
            tickets = tickets.Where(ticket => ticket.OrderId == orderId);
        }

        return await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Select(ticket => new ShopAfterSalesTicketDto(
                ticket.Id,
                ticket.OrderId,
                ticket.StoreId,
                ticket.OrderLineId,
                ticket.Type.ToString(),
                ticket.Quantity,
                ticket.Reason,
                ticket.Status.ToString(),
                ticket.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<ShopAfterSalesTicketDto> Handle(
        CreateShopAfterSalesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        bool ownsOrder = await dbContext.SalesOrders.AsNoTracking().AnyAsync(order =>
                order.Id == command.OrderId
                && order.CustomerTenantId == access.CustomerTenantId
                && access.StoreIds.Contains(order.StoreId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!ownsOrder)
        {
            throw new NotFoundException($"Order {command.OrderId} not found.");
        }

        var created = await mediator.Send(new CreateAfterSalesTicketCommand(
                command.OrderId,
                command.OrderLineId,
                command.Type,
                command.Quantity,
                command.Reason), cancellationToken)
            .ConfigureAwait(false);
        return new ShopAfterSalesTicketDto(
            created.Id,
            created.OrderId,
            created.StoreId,
            created.OrderLineId,
            created.Type,
            created.Quantity,
            created.Reason,
            created.Status,
            created.CreatedAt);
    }
}
