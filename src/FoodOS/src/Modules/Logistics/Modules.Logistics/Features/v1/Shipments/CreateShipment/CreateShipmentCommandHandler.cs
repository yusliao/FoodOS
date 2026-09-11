using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Features.v1;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.CreateShipment;

public sealed class CreateShipmentCommandHandler(LogisticsDbContext dbContext, IMediator mediator)
    : ICommandHandler<CreateShipmentCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(CreateShipmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await dbContext.Routes
            .FirstOrDefaultAsync(r => r.Id == command.RouteId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Route {command.RouteId} not found.");

        if (route.WarehouseId != command.WarehouseId)
        {
            throw new CustomException(
                "Route does not belong to the requested warehouse.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        _ = await dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Vehicle {command.VehicleId} not found.");

        _ = await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.Id == command.DriverId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Driver {command.DriverId} not found.");

        DateOnly businessDate = command.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await dbContext.Shipments
            .FirstOrDefaultAsync(
                s => s.RouteId == route.Id && s.BusinessDate == businessDate,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.ToDto();
        }

        var packed = await mediator.Send(
                new ListPackedOrdersQuery(command.WarehouseId, businessDate, route.GetStoreIds()),
                cancellationToken)
            .ConfigureAwait(false);
        if (packed.Count == 0)
        {
            throw new CustomException(
                "No packed orders on this route for the business date.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var picked = await mediator.Send(
                new ListPickedLotsForOrdersQuery(packed.Select(o => o.Id).ToList()),
                cancellationToken)
            .ConfigureAwait(false);

        string number = await ShipmentNumbers
            .NextAsync(dbContext, route.Code, businessDate, cancellationToken)
            .ConfigureAwait(false);

        var shipment = Shipment.Create(
            number, route.Id, command.WarehouseId, businessDate, command.VehicleId, command.DriverId);

        var ordersByStore = packed.ToLookup(o => o.StoreId);
        int seq = 1;
        foreach (var storeId in route.GetStoreIds())
        {
            if (!ordersByStore.Contains(storeId))
            {
                continue;
            }

            var stop = shipment.AddStop(storeId, seq++, null);
            dbContext.ShipmentStops.Add(stop);

            foreach (var order in ordersByStore[storeId])
            {
                var line = shipment.AddLine(order.Id, storeId);
                dbContext.ShipmentLines.Add(line);
                foreach (var lot in picked.Where(p => p.OrderId == order.Id))
                {
                    var slice = line.AddLot(
                        lot.OrderLineId, lot.ProductId, lot.Zone, lot.LotId, lot.LotNo, lot.Quantity);
                    dbContext.ShipmentLineLots.Add(slice);
                }
            }
        }

        if (shipment.Lines.Count == 0)
        {
            throw new CustomException(
                "No packed orders on this route for the business date.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return shipment.ToDto();
    }
}
