using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts;
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
        var previous = order.Lines
            .Select(l => (l.Id, l.ReservationId, l.ProductId, l.Zone, l.OrderedQty, l.UnitPrice, l.Currency))
            .ToList();
        int previousRevision = order.Revision;

        order.BeginAmend(utcNow);

        foreach (var (lineId, reservationId, _, _, _, _, _) in previous)
        {
            if (reservationId is { } id)
            {
                await InventoryStockOps.UnreserveAsync(
                        mediator,
                        id,
                        order.Id,
                        lineId,
                        previousRevision,
                        "amend-unreserve",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var draftLines = new List<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)>();
        var zones = new Dictionary<Guid, TemperatureZoneKind>();
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
            zones[line.ProductId] = zone;
        }

        ReplaceOrderLines(dbContext, order, draftLines);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var reserved = new List<(Guid LineId, Guid ReservationId)>();
        try
        {
            foreach (var line in order.Lines)
            {
                Guid reservationId = await InventoryStockOps.ReserveAsync(
                        mediator,
                        order.WarehouseId,
                        zones[line.ProductId],
                        line.ProductId,
                        line.OrderedQty,
                        order.Id,
                        line.Id,
                        order.Revision,
                        cancellationToken)
                    .ConfigureAwait(false);
                line.BindReservation(reservationId, line.OrderedQty);
                reserved.Add((line.Id, reservationId));
            }

            order.CompleteAmend();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return order.Id;
        }
        catch (Exception)
        {
            foreach (var (lineId, reservationId) in reserved)
            {
                await InventoryStockOps.UnreserveAsync(
                        mediator,
                        reservationId,
                        order.Id,
                        lineId,
                        order.Revision,
                        "amend-compensate",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            ReplaceOrderLines(
                dbContext,
                order,
                previous.Select(p => (p.ProductId, p.Zone, p.OrderedQty, p.UnitPrice, p.Currency)).ToList());
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var line in order.Lines)
            {
                if (!Enum.TryParse(line.Zone, ignoreCase: true, out TemperatureZoneKind zone))
                {
                    continue;
                }

                Guid restored = await InventoryStockOps.ReserveAsync(
                        mediator,
                        order.WarehouseId,
                        zone,
                        line.ProductId,
                        line.OrderedQty,
                        order.Id,
                        line.Id,
                        order.Revision,
                        cancellationToken)
                    .ConfigureAwait(false);
                line.BindReservation(restored, line.OrderedQty);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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
