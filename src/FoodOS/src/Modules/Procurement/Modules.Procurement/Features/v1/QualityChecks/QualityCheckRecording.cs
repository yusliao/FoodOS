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

        // Serialize QC for the whole order: different lots also update the same line totals.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string lockKey = $"procurement:qc:{purchaseOrderId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken).ConfigureAwait(false);

        var po = await dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {purchaseOrderId} not found.");

        po.EnsureCanRecordQualityCheck();
        var line = po.RequireLine(lineId);
        var zone = ZoneKinds.Parse(line.Zone);
        string normalizedLotNo = lotNo.Trim().ToUpperInvariant();
        var recorded = po.QualityChecks.FirstOrDefault(check =>
            check.LineId == lineId && check.Result == result && check.LotNo == normalizedLotNo);
        if (recorded is not null && (recorded.Quantity != quantity || recorded.SampleQty != sampleQty
            || recorded.Note != (string.IsNullOrWhiteSpace(note) ? null : note.Trim())
            || !recorded.PhotoIds().SequenceEqual(photoFileIds ?? [])))
        {
            throw new CustomException(
                "This purchase line and lot already have a quality check with different details.",
                (IEnumerable<string>?)null,
                System.Net.HttpStatusCode.Conflict);
        }

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

        if (recorded is not null)
        {
            if (recorded.LotId != lotId)
                throw new CustomException("The recorded quality check does not match its inventory receipt.",
                    (IEnumerable<string>?)null, System.Net.HttpStatusCode.Conflict);
            await EnsurePutawayAsync(mediator, po, line, recorded, lotId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return recorded.Id;
        }

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
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await EnsurePutawayAsync(mediator, po, line, check, lotId, cancellationToken).ConfigureAwait(false);
        return check.Id;
    }

    private static async Task EnsurePutawayAsync(
        IMediator mediator, PurchaseOrder po, PurchaseOrderLine line, QualityCheck check,
        Guid lotId, CancellationToken cancellationToken)
    {
        if (check.Result == QualityCheckResult.Pass)
        {
            await mediator.Send(
                    new CreatePutawayTaskCommand(
                        po.WarehouseId,
                        line.Zone,
                        line.ProductId,
                        lotId,
                        check.Quantity,
                        Source: "QcPass",
                        RefId: check.Id),
                    cancellationToken)
                .ConfigureAwait(false);
        }

    }
}
