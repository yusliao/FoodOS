using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.AmendOrder;

public sealed class AmendOrderCommandHandler(OrderingDbContext dbContext, IMediator mediator, TimeProvider clock)
    : ICommandHandler<AmendOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(AmendOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        DateTimeOffset utcNow = clock.GetUtcNow();
        order.BeginAmend(utcNow);

        var draftLines = new List<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)>();
        var products = await ShopCatalog.GetActiveManyAsync(
                mediator,
                command.Lines.Select(line => line.ProductId),
                cancellationToken)
            .ConfigureAwait(false);
        var quotes = await ShopCatalog.QuoteManyAsync(
                mediator,
                order.CustomerOrgId,
                command.Lines.Select(line => (line.ProductId, line.Quantity)),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var line in command.Lines)
        {
            var (_, zone) = products[line.ProductId];
            var quote = quotes[line.ProductId];
            draftLines.Add((line.ProductId, zone.ToString(), line.Quantity, quote.UnitPrice, quote.Currency));
        }

        ReplaceOrderLines(dbContext, order, draftLines);
        foreach (var line in order.Lines)
        {
            line.BindReservation(order.Id, line.OrderedQty);
        }

        order.CompleteAmend();
        order.MarkWarehouseNotificationPending("Order amendment accepted by FoodOS; warehouse confirmation is pending.", utcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.Id;
    }

    private static void ReplaceOrderLines(
        OrderingDbContext dbContext,
        SalesOrder order,
        IReadOnlyList<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)> lines)
    {
        var existing = order.Lines.ToList();
        if (existing.Count > 0)
        {
            dbContext.SalesOrderLines.RemoveRange(existing);
        }

        order.ReplaceLines(lines);
        foreach (var line in order.Lines)
        {
            dbContext.SalesOrderLines.Add(line);
        }
    }
}
