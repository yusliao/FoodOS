using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Features.v1;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.DepartShipment;

public sealed class DepartShipmentCommandHandler(
    LogisticsDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<DepartShipmentCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(DepartShipmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shipment = await dbContext.Shipments
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipment {command.ShipmentId} not found.");

        if (shipment.Status is ShipmentStatus.Departed or ShipmentStatus.Completed)
        {
            return shipment.ToDto();
        }

        shipment.Depart();
        string actor = currentUser.GetUserId().ToString("N");

        foreach (var line in shipment.Lines)
        {
            var lots = new List<OrderShipmentLot>();
            foreach (var lot in line.Lots)
            {
                await mediator.Send(
                        new ShipPickedStockCommand(
                            shipment.WarehouseId,
                            ZoneKinds.Parse(lot.Zone),
                            lot.ProductId,
                            lot.LotId,
                            lot.Quantity,
                            $"ship:{shipment.Id:N}:{lot.Id:N}",
                            shipment.Id),
                        cancellationToken)
                    .ConfigureAwait(false);

                lots.Add(new OrderShipmentLot(lot.OrderLineId, lot.LotId, lot.LotNo, lot.Quantity));

                var trace = TraceEvent.Capture(
                    lot.ProductId,
                    "shipping",
                    "in_transit",
                    lot.Quantity,
                    "EA",
                    actor,
                    "Shipment",
                    shipment.Id,
                    lot.LotId);
                dbContext.TraceEvents.Add(trace);
            }

            await mediator.Send(new StartOrderInTransitCommand(line.OrderId, lots), cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return shipment.ToDto();
    }
}
