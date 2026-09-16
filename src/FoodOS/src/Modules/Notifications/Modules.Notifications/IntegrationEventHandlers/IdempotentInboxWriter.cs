using FSH.Framework.Web.Realtime;
using FSH.Modules.Notifications.Data;
using FSH.Modules.Notifications.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

// Callers own audience authorization and stable IDs. This writer only persists and signals in the current tenant.
public sealed class IdempotentInboxWriter(NotificationsDbContext db, IHubContext<AppHub> hub)
{
    public async Task WriteAsync(Notification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (await db.Notifications.AnyAsync(n => n.Id == notification.Id && n.UserId == notification.UserId, ct)
            .ConfigureAwait(false)) return;
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_Notifications" })
        {
            db.Entry(notification).State = EntityState.Detached;
            if (await db.Notifications.AnyAsync(n => n.Id == notification.Id && n.UserId == notification.UserId, ct)
                .ConfigureAwait(false)) return;
            throw;
        }

        // Persist first. Realtime is a hint, not durable delivery or a substitute for inbox reads.
        await hub.Clients.Group($"user:{notification.UserId}").SendAsync("NotificationCreated", new
        {
            id = notification.Id, type = notification.Type, title = notification.Title,
            body = notification.Body, link = notification.Link, source = notification.Source,
            createdAtUtc = notification.CreatedAtUtc,
        }, ct).ConfigureAwait(false);
    }
}
