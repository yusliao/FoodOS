using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks;

internal static class QualityCheckRecording
{
    public static async Task<Guid> RecordAsync(
        ProcurementDbContext dbContext,
        IMediator mediator,
        ICurrentUser currentUser,
        Guid purchaseOrderId,
        Guid lineId,
        QualityCheckResult result,
        decimal quantity,
        decimal sampleQty,
        string lotNo,
        DateOnly expiryDate,
        DateOnly? manufacturedOn,
        string? note,
        IReadOnlyList<Guid>? photoFileIds,
        CancellationToken cancellationToken)
    {
        var inspectorId = currentUser.GetUserId();
        if (inspectorId == Guid.Empty)
        {
            throw new CustomException(
                "Cannot record a quality check without an authenticated inspector.",
                (IEnumerable<string>?)null,
                System.Net.HttpStatusCode.Unauthorized);
        }

        var po = await dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {purchaseOrderId} not found.");

        po.EnsureCanRecordQualityCheck();
        var line = po.RequireLine(lineId);
        var zone = ZoneKinds.Parse(line.Zone);

        Guid lotId = result == QualityCheckResult.Pass
            ? await InventoryStockOps.ReceiveAsync(
                mediator,
                po.WarehouseId,
                zone,
                line.ProductId,
                lotNo,
                expiryDate,
                quantity,
                po.Id,
                line.Id,
                manufacturedOn,
                origin: null,
                cancellationToken).ConfigureAwait(false)
            : await InventoryStockOps.ReceiveIsolatedAsync(
                mediator,
                po.WarehouseId,
                zone,
                line.ProductId,
                lotNo,
                expiryDate,
                quantity,
                po.Id,
                line.Id,
                manufacturedOn,
                origin: null,
                po.SupplierId,
                cancellationToken).ConfigureAwait(false);

        var check = po.RecordQualityCheck(
            lineId,
            inspectorId,
            result,
            sampleQty,
            quantity,
            lotNo,
            lotId,
            note,
            photoFileIds);

        dbContext.QualityChecks.Add(check);
        dbContext.ReceiveRecords.Add(po.ReceiveRecords.First(r => r.QualityCheckId == check.Id));

        dbContext.TraceEvents.Add(TraceEvent.Capture(
            line.ProductId,
            bizStep: "receiving",
            disposition: result == QualityCheckResult.Pass ? "active" : "quarantine",
            quantity,
            uom: "EA",
            actorUserId: inspectorId.ToString(),
            refType: "QualityCheck",
            refId: check.Id,
            lotId,
            destLocation: po.WarehouseId.ToString("N")));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (result == QualityCheckResult.Pass)
        {
            await mediator.Send(
                    new CreatePutawayTaskCommand(
                        po.WarehouseId,
                        line.Zone,
                        line.ProductId,
                        lotId,
                        quantity,
                        Source: "QcPass",
                        RefId: check.Id),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return check.Id;
    }
}
