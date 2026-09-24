using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;

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
        var duplicate = await db.InboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provider == envelope.Provider
                && x.ConnectionId == envelope.ConnectionId
                && x.ExternalEventId == envelope.ExternalEventId, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
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
}
