using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.Events;
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
    ICurrentUser currentUser,
    IEventBus eventBus,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<ConfirmPodCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(ConfirmPodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shipment = await dbContext.Shipments
            .FirstOrDefaultAsync(s => s.Stops.Any(st => st.Id == command.StopId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Stop {command.StopId} not found.");

        Guid userId = currentUser.GetUserId();
        bool assignedToCurrentDriver = await dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(driver => driver.Id == shipment.DriverId && driver.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!assignedToCurrentDriver && !currentUser.IsInRole(RoleConstants.Admin))
        {
            throw new NotFoundException($"Stop {command.StopId} not found.");
        }

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

        if (command.Lines.Count != expectedLots.Count
            || command.Lines.Select(l => (l.OrderLineId, l.LotId)).Distinct().Count() != command.Lines.Count)
        {
            throw new CustomException("Signature lines must match the shipped lots exactly.",
                (IEnumerable<string>?)null, HttpStatusCode.BadRequest);
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

            if (signed[(lot.OrderLineId, lot.LotId)].SignedQty < 0
                || signed[(lot.OrderLineId, lot.LotId)].SignedQty > lot.Quantity)
            {
                throw new CustomException("Signed quantity must be between zero and shipped quantity.",
                    (IEnumerable<string>?)null, HttpStatusCode.BadRequest);
            }
        }

        string signedJson = JsonSerializer.Serialize(command.Lines.OrderBy(l => l.OrderLineId).ThenBy(l => l.LotId));
        var photoIds = (command.PhotoFileIds ?? []).OrderBy(id => id).ToArray();
        bool isNewSignature = stop.ProofOfDelivery is null;
        var pod = shipment.PrepareStopSignature(command.StopId, signedJson, photoIds, command.SignerName, command.Geo);
        if (isNewSignature)
        {
            dbContext.ProofOfDeliveries.Add(pod);
            foreach (var line in stopLines)
            {
                foreach (var lot in line.Lots)
                {
                    decimal quantity = lot.Quantity - signed[(lot.OrderLineId, lot.LotId)].SignedQty;
                    if (quantity > 0)
                    {
                        // The shipment-lot identity remains stable across retries.
                        dbContext.ReturnsOnTruck.Add(shipment.RecordReturn(
                            line.OrderId, lot.ProductId, lot.LotId, quantity, "partial-reject", lot.Id));
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        string actor = currentUser.GetUserId().ToString("N");
        var receiptsByOrder = new Dictionary<Guid, List<OrderLineReceipt>>();

        foreach (var line in stopLines)
        {
            var receipts = new List<OrderLineReceipt>();
            foreach (var lot in line.Lots)
            {
                var sign = signed[(lot.OrderLineId, lot.LotId)];
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
                    var ret = shipment.Returns.Single(r => r.Id == lot.Id);

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

            if (!receiptsByOrder.TryGetValue(line.OrderId, out var orderReceipts))
            {
                orderReceipts = [];
                receiptsByOrder.Add(line.OrderId, orderReceipts);
            }

            orderReceipts.AddRange(receipts);
        }

        foreach (var (orderId, receipts) in receiptsByOrder)
        {
            await mediator.Send(new ConfirmOrderReceivedCommand(orderId, receipts), cancellationToken)
                .ConfigureAwait(false);
        }

        shipment.ConfirmStop(command.StopId, signedJson, photoIds, command.SignerName, command.Geo);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
                new ShipmentStopDeliveredIntegrationEvent(
                    Id: Guid.CreateVersion7(),
                    OccurredOnUtc: DateTime.UtcNow,
                    TenantId: tenantAccessor.MultiTenantContext.TenantInfo?.Id,
                    CorrelationId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    Source: "Logistics",
                    ShipmentId: shipment.Id,
                    StopId: stop.Id,
                    StoreId: stop.StoreId,
                    OrderIds: receiptsByOrder.Keys.ToList()),
                cancellationToken)
            .ConfigureAwait(false);

        return shipment.ToDto();
    }
}
