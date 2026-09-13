using FSH.Framework.Core.Exceptions;
using FSH.Framework.Web.Realtime;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Notifications.Data;
using FSH.Modules.Notifications.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class OperationalInboxWriter(
    NotificationsDbContext db,
    IHubContext<AppHub> hub,
    IUserService userService,
    ILogger<OperationalInboxWriter> logger)
{
    public async Task FanoutAsync(
        string permission,
        string type,
        string title,
        string? body,
        string link,
        string source,
        object? metadata,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var users = await userService.GetListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var user in users)
        {
            if (!user.IsActive || string.IsNullOrWhiteSpace(user.Id))
            {
                continue;
            }

            if (await db.Notifications
                .AnyAsync(n => n.UserId == user.Id && n.Type == type && n.Link == link, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            bool allowed;
            try
            {
                allowed = await userService.HasPermissionAsync(user.Id, permission, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedException)
            {
                continue;
            }

            if (!allowed)
            {
                continue;
            }

            var notification = Notification.Create(
                userId: user.Id,
                type: type,
                title: title,
                body: body,
                link: link,
                source: source,
                metadata: metadata);

            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await hub.Clients.Group($"user:{user.Id}")
                .SendAsync("NotificationCreated", new
                {
                    id = notification.Id,
                    type = notification.Type,
                    title = notification.Title,
                    body = notification.Body,
                    link = notification.Link,
                    source = notification.Source,
                    createdAtUtc = notification.CreatedAtUtc,
                }, cancellationToken)
                .ConfigureAwait(false);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Operational inbox fanout type {Type} link {Link}", type, link);
        }
    }
}
