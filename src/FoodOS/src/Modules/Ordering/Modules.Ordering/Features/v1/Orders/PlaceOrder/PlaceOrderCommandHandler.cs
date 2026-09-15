using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;

public sealed class PlaceOrderCommandHandler(OrderingDbContext dbContext, IMediator mediator, TimeProvider clock)
    : ICommandHandler<PlaceOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var store = await dbContext.Stores
            .FirstOrDefaultAsync(s => s.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {command.StoreId} not found.");

        var org = await dbContext.CustomerOrgs
            .FirstOrDefaultAsync(o => o.Id == store.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Customer organization {store.CustomerOrgId} not found.");

        if (org.CreditHold)
        {
            throw new CustomException(
                "Customer is on credit hold.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var cart = await dbContext.Carts
            .FirstOrDefaultAsync(c => c.StoreId == store.Id, cancellationToken)
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

        var draftLines = new List<(Guid ProductId, string Zone, decimal Qty, decimal UnitPrice, string Currency)>();
        var zones = new Dictionary<Guid, TemperatureZoneKind>();
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
        foreach (var line in cart.Lines)
        {
            var (_, zone) = products[line.ProductId];
            var quote = quotes[line.ProductId];
            draftLines.Add((line.ProductId, zone.ToString(), line.Quantity, quote.UnitPrice, quote.Currency));
            zones[line.ProductId] = zone;
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
            store.CustomerTenantId);
        dbContext.SalesOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var reserved = new List<(Guid LineId, Guid ReservationId)>();
        try
        {
            foreach (var line in order.Lines)
            {
                Guid reservationId = await InventoryStockOps.ReserveAsync(
                        mediator,
                        warehouse.Id,
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

            order.Place(utcNow);
            dbContext.CartLines.RemoveRange(cart.Lines);
            cart.Clear();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return order.Id;
        }
        catch (Exception)
        {
            // Compensation must survive request cancellation and discard unsaved cart/order changes.
            using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            dbContext.ChangeTracker.Clear();
            var persistedOrder = await dbContext.SalesOrders
                .FirstAsync(o => o.Id == order.Id, compensation.Token)
                .ConfigureAwait(false);
            if (persistedOrder.Status != SalesOrderStatus.Draft)
            {
                // A committed placement must not have its reservations released.
                throw;
            }

            foreach (var (lineId, reservationId) in reserved)
            {
                await InventoryStockOps.UnreserveAsync(
                        mediator,
                        reservationId,
                        order.Id,
                        lineId,
                        order.Revision,
                        "place-compensate",
                        compensation.Token)
                    .ConfigureAwait(false);
            }

            persistedOrder.FailPlace();
            await dbContext.SaveChangesAsync(compensation.Token).ConfigureAwait(false);
            throw;
        }
    }
}
