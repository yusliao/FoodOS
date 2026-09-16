using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Notifications.Contracts.Authorization;
using FSH.Modules.Notifications.Domain;
using FSH.Modules.Notifications.Features.v1.Internal;
using FSH.Modules.Tickets.Contracts.Authorization;
using FSH.Modules.Tickets.Contracts.Notifications;

namespace FSH.Modules.Notifications.IntegrationEventHandlers;

public sealed class TicketActivityNotificationHandler(
    IdempotentInboxWriter inbox,
    ITicketNotificationAudience tickets,
    IUserProfileService users,
    IUserPermissionService permissions,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor) : IIntegrationEventHandler<TicketActivityIntegrationEvent>
{
    public async Task HandleAsync(TicketActivityIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId != @event.TenantId
            || string.IsNullOrWhiteSpace(@event.CustomerTenantId) || @event.Id == Guid.Empty
            || (tenantId != MultitenancyConstants.Root.Id && tenantId != @event.CustomerTenantId)
            || (@event.OriginTenantId != MultitenancyConstants.Root.Id && @event.OriginTenantId != @event.CustomerTenantId)
            || @event.Source != "Tickets" || !Enum.IsDefined(@event.Activity))
        {
            throw new InvalidOperationException("Invalid ticket notification delivery scope.");
        }

        var audience = await tickets.ResolveAsync(@event.TicketId, @event.CustomerTenantId, @event.Activity, ct)
            .ConfigureAwait(false);
        if (audience is null) return;
        var participants = audience.ParticipantUserIds.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        var candidates = new HashSet<string>(participants, StringComparer.Ordinal);
        if (audience.NotifyOperatorQueue)
        {
            var queueUsers = await users.GetListAsync(ct).ConfigureAwait(false);
            candidates.UnionWith(queueUsers.Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Id)).Select(u => u.Id!));
        }
        var active = await users.GetActiveUserIdsAsync(candidates, ct).ConfigureAwait(false);
        foreach (var userId in active)
        {
            if ((tenantId == @event.OriginTenantId && userId == @event.ActorUserId.ToString())
                || !await permissions.HasPermissionAsync(userId, TicketsPermissions.Tickets.View, ct).ConfigureAwait(false)
                || !await permissions.HasPermissionAsync(userId, NotificationPermissions.Inbox.View, ct).ConfigureAwait(false)
                || (!participants.Contains(userId)
                    && !await permissions.HasPermissionAsync(userId, TicketsPermissions.Tickets.Assign, ct).ConfigureAwait(false)))
            {
                continue;
            }
            await DeliverAsync(@event, userId, ct).ConfigureAwait(false);
        }
    }

    private async Task DeliverAsync(TicketActivityIntegrationEvent activity, string userId, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ticket-notification:{activity.Id:N}:{userId}"));
        var id = new Guid(hash.AsSpan(0, 16));
        var type = activity.Activity switch
        {
            TicketActivityKind.Created => "tickets.created",
            TicketActivityKind.Assigned => "tickets.assigned",
            TicketActivityKind.CommentAdded => "tickets.comment",
            TicketActivityKind.StatusChanged => "tickets.status-changed",
            _ => throw new InvalidOperationException("Unknown ticket activity."),
        };
        var notification = Notification.Create(userId, type,
            TicketNotificationText.Title(type, CultureInfo.GetCultureInfo("en-US"))!,
            body: null, link: $"/tickets/{activity.TicketId}", source: "Tickets",
            metadata: new { ticketId = activity.TicketId, activity = type }, notificationId: id);
        await inbox.WriteAsync(notification, ct).ConfigureAwait(false);
    }
}
