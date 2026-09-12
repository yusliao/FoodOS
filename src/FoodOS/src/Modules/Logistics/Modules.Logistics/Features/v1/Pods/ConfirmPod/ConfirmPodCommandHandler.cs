using System.Net;
using System.Text.Json;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Features.v1;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Pods.ConfirmPod;

public sealed class ConfirmPodCommandHandler(
    LogisticsDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<ConfirmPodCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(ConfirmPodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shipment = await dbContext.Shipments
            .FirstOrDefaultAsync(s => s.Stops.Any(st => st.Id == command.StopId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Stop {command.StopId} not found.");

        var stop = shipment.Stops.First(s => s.Id == command.StopId);
        if (stop.Status == StopStatus.Delivered)
        {
            return shipment.ToDto();
        }

        var stopLines = shipment.Lines.Where(l => l.StoreId == stop.StoreId).ToList();
        var expectedLots = stopLines.SelectMany(l => l.Lots).ToList();
        if (expectedLots.Count == 0)
        {
            throw new CustomException(
                "Stop has no shipped lots to sign.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var signed = command.Lines.ToDictionary(l => (l.OrderLineId, l.LotId));
        foreach (var lot in expectedLots)
        {
            if (!signed.ContainsKey((lot.OrderLineId, lot.LotId)))
            {
                throw new CustomException(
                    "Every shipped lot on this stop must be signed.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            }
        }

        string signedJson = JsonSerializer.Serialize(command.Lines);
        var photoIds = command.PhotoFileIds ?? [];
        var pod = shipment.ConfirmStop(command.StopId, signedJson, photoIds, command.SignerName, command.Geo);
        dbContext.ProofOfDeliveries.Add(pod);

        string actor = currentUser.GetUserId().ToString("N");
        var receiptsByOrder = new Dictionary<Guid, List<OrderLineReceipt>>();

        foreach (var line in stopLines)
        {
            var receipts = new List<OrderLineReceipt>();
            foreach (var lot in line.Lots)
            {
                var sign = signed[(lot.OrderLineId, lot.LotId)];
                if (sign.SignedQty > lot.Quantity)
                {
                    throw new CustomException(
                        "Signed quantity cannot exceed shipped quantity.",
                        (IEnumerable<string>?)null,
                        HttpStatusCode.BadRequest);
                }

                decimal returnedQty = lot.Quantity - sign.SignedQty;
                string? reason = returnedQty > 0 ? "partial-reject" : null;

                if (sign.SignedQty > 0)
                {
                    await mediator.Send(
                            new DeliverInTransitStockCommand(
                                shipment.WarehouseId,
                                ZoneKinds.Parse(lot.Zone),
                                lot.ProductId,
                                lot.LotId,
                                sign.SignedQty,
                                $"deliver:{pod.Id:N}:{lot.Id:N}",
                                shipment.Id),
                            cancellationToken)
                        .ConfigureAwait(false);

                    dbContext.TraceEvents.Add(TraceEvent.Capture(
                        lot.ProductId,
                        "arriving",
                        "sold",
                        sign.SignedQty,
                        "EA",
                        actor,
                        "ProofOfDelivery",
                        pod.Id,
                        lot.LotId));
                }

                if (returnedQty > 0)
                {
                    var ret = shipment.RecordReturn(
                        line.OrderId, lot.ProductId, lot.LotId, returnedQty, reason ?? "partial-reject");
                    dbContext.ReturnsOnTruck.Add(ret);

                    await mediator.Send(
                            new ReturnInTransitStockCommand(
                                shipment.WarehouseId,
                                ZoneKinds.Parse(lot.Zone),
                                lot.ProductId,
                                lot.LotId,
                                returnedQty,
                                $"return:{pod.Id:N}:{lot.Id:N}",
                                shipment.Id),
                            cancellationToken)
                        .ConfigureAwait(false);

                    dbContext.TraceEvents.Add(TraceEvent.Capture(
                        lot.ProductId,
                        "returning",
                        "active",
                        returnedQty,
                        "EA",
                        actor,
                        "ReturnOnTruck",
                        ret.Id,
                        lot.LotId));

                    await mediator.Send(
                            new CreatePutawayTaskCommand(
                                shipment.WarehouseId,
                                lot.Zone,
                                lot.ProductId,
                                lot.LotId,
                                returnedQty,
                                Source: "ReturnOnTruck",
                                RefId: ret.Id),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                receipts.Add(new OrderLineReceipt(
                    lot.OrderLineId, lot.LotId, sign.SignedQty, returnedQty, reason));
            }

            receiptsByOrder[line.OrderId] = receipts;
        }

        foreach (var (orderId, receipts) in receiptsByOrder)
        {
            await mediator.Send(new ConfirmOrderReceivedCommand(orderId, receipts), cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return shipment.ToDto();
    }
}
