using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsInboxService(WmsIntegrationDbContext db)
{
    public async Task<WmsEventReceipt> ReceiveAsync(
        WmsEventEnvelope envelope,
        string rawJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(rawJson);
        var duplicate = await db.InboxMessages
            .FirstOrDefaultAsync(x => x.Provider == envelope.Provider
                && x.ConnectionId == envelope.ConnectionId
                && x.ExternalEventId == envelope.ExternalEventId, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            if (duplicate.Status == "awaitingGap")
            {
                var waitingCursor = await db.ObjectCursors
                    .FirstOrDefaultAsync(x => x.Provider == envelope.Provider
                        && x.ConnectionId == envelope.ConnectionId
                        && x.EntityType == envelope.EntityType
                        && x.ExternalObjectId == envelope.ExternalObjectId, cancellationToken)
                    .ConfigureAwait(false);
                if (waitingCursor is not null && envelope.Sequence == waitingCursor.LastAcceptedSequence + 1)
                {
                    waitingCursor.Advance(envelope.Sequence);
                    duplicate.AcceptAfterGap();
                    await ApplyInventoryProjectionAsync(envelope, cancellationToken).ConfigureAwait(false);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return new(envelope.MessageId, "accepted", waitingCursor.LastAcceptedSequence, null);
                }
            }
            return new(envelope.MessageId, "duplicate", duplicate.Sequence, "Event was already accepted.");
        }

        var cursor = await db.ObjectCursors
            .FirstOrDefaultAsync(x => x.Provider == envelope.Provider
                && x.ConnectionId == envelope.ConnectionId
                && x.EntityType == envelope.EntityType
                && x.ExternalObjectId == envelope.ExternalObjectId, cancellationToken)
            .ConfigureAwait(false);

        string status;
        string? detail = null;
        if (cursor is null)
        {
            status = "accepted";
            cursor = WmsObjectCursor.Create(
                envelope.Provider, envelope.ConnectionId, envelope.EntityType, envelope.ExternalObjectId, envelope.Sequence);
            db.ObjectCursors.Add(cursor);
        }
        else if (envelope.Sequence <= cursor.LastAcceptedSequence)
        {
            status = "stale";
            detail = "An equal or newer object version was already accepted.";
        }
        else if (envelope.Sequence > cursor.LastAcceptedSequence + 1)
        {
            status = "awaitingGap";
            detail = $"Expected sequence {cursor.LastAcceptedSequence + 1}; incremental pull is required.";
        }
        else
        {
            status = "accepted";
            cursor.Advance(envelope.Sequence);
        }

        db.InboxMessages.Add(WmsInboxMessage.Create(
            envelope.MessageId,
            envelope.Provider,
            envelope.ConnectionId,
            envelope.EventType,
            envelope.EntityType,
            envelope.ExternalEventId,
            envelope.ExternalObjectId,
            envelope.Sequence,
            envelope.IdempotencyKey,
            envelope.CorrelationId,
            envelope.SchemaVersion,
            rawJson,
            status,
            envelope.OccurredAt,
            detail));

        if (status == "accepted")
        {
            await ApplyInventoryProjectionAsync(envelope, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var raced = await db.InboxMessages.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Provider == envelope.Provider
                    && x.ConnectionId == envelope.ConnectionId
                    && x.ExternalEventId == envelope.ExternalEventId, cancellationToken)
                .ConfigureAwait(false);
            if (raced is null) throw;
            return new(envelope.MessageId, "duplicate", raced.Sequence, "Event was already accepted.");
        }

        return new(envelope.MessageId, status, cursor.LastAcceptedSequence, detail);
    }

    private async Task ApplyInventoryProjectionAsync(
        WmsEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.EventType is not WmsEventTypes.InventorySnapshot
            and not WmsEventTypes.InventoryChanged
            and not WmsEventTypes.InventoryAdjusted)
        {
            return;
        }

        string provider = Normalize(envelope.Provider);
        string connectionId = Normalize(envelope.ConnectionId);
        var balance = await db.InventoryBalances
            .FirstOrDefaultAsync(x => x.Provider == provider
                && x.ConnectionId == connectionId
                && x.ExternalObjectId == envelope.ExternalObjectId, cancellationToken)
            .ConfigureAwait(false);

        JsonElement payload = envelope.Payload;
        string? lotNumber = payload.TryGetProperty("lotNumber", out JsonElement lot)
            && lot.ValueKind == JsonValueKind.String
                ? lot.GetString()
                : null;
        if (balance is null)
        {
            db.InventoryBalances.Add(WmsInventoryBalance.Create(
                envelope.Provider,
                envelope.ConnectionId,
                envelope.ExternalObjectId,
                payload.GetProperty("warehouseId").GetString()!,
                payload.GetProperty("ownerId").GetString()!,
                payload.GetProperty("sku").GetString()!,
                payload.GetProperty("uom").GetString()!,
                lotNumber,
                payload.GetProperty("onHandQuantity").GetDecimal(),
                payload.GetProperty("allocatedQuantity").GetDecimal(),
                payload.GetProperty("availableQuantity").GetDecimal(),
                payload.GetProperty("quarantinedQuantity").GetDecimal(),
                envelope.Sequence,
                envelope.OccurredAt));
            return;
        }

        balance.Apply(
            payload.GetProperty("warehouseId").GetString()!,
            payload.GetProperty("ownerId").GetString()!,
            payload.GetProperty("sku").GetString()!,
            payload.GetProperty("uom").GetString()!,
            lotNumber,
            payload.GetProperty("onHandQuantity").GetDecimal(),
            payload.GetProperty("allocatedQuantity").GetDecimal(),
            payload.GetProperty("availableQuantity").GetDecimal(),
            payload.GetProperty("quarantinedQuantity").GetDecimal(),
            envelope.Sequence,
            envelope.OccurredAt);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
