using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Tickets.Contracts.Notifications;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Domain.Events;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Events;

public sealed class TicketEventHandlers(
    TicketsDbContext db,
    IEventBus events,
    ICurrentUser currentUser,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor) :
    INotificationHandler<TicketAssignedDomainEvent>,
    INotificationHandler<TicketCommentAddedDomainEvent>,
    INotificationHandler<TicketCreatedDomainEvent>,
    INotificationHandler<TicketStatusChangedDomainEvent>
{
    public ValueTask Handle(TicketAssignedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishAsync(notification.TicketId, notification.EventId, notification.OccurredOnUtc,
            currentUser.GetUserId(), TicketActivityKind.Assigned, cancellationToken);
    }

    public ValueTask Handle(TicketCommentAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishAsync(notification.TicketId, notification.EventId, notification.OccurredOnUtc,
            notification.AuthorUserId, TicketActivityKind.CommentAdded, cancellationToken);
    }

    public ValueTask Handle(TicketCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishAsync(notification.TicketId, notification.EventId, notification.OccurredOnUtc,
            notification.ReporterUserId, TicketActivityKind.Created, cancellationToken);
    }

    public ValueTask Handle(TicketStatusChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishAsync(notification.TicketId, notification.EventId, notification.OccurredOnUtc,
            currentUser.GetUserId(), TicketActivityKind.StatusChanged, cancellationToken);
    }

    private async ValueTask PublishAsync(Guid ticketId, Guid eventId, DateTimeOffset occurredOn,
        Guid actorUserId, TicketActivityKind activity, CancellationToken cancellationToken)
    {
        var origin = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(origin)) return;
        var owner = await db.Tickets.AsNoTracking().Where(t => t.Id == ticketId)
            .Select(t => t.CustomerTenantId).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (owner is null || (origin != MultitenancyConstants.Root.Id && origin != owner)) return;

        // The existing bus establishes isolated delivery scopes. Never change this request's DbContext tenant.
        foreach (var destination in new[] { MultitenancyConstants.Root.Id, owner }.Distinct(StringComparer.Ordinal))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ticket-activity:{eventId:N}:{destination}"));
            var deliveryId = new Guid(hash.AsSpan(0, 16));
            await events.PublishAsync(new TicketActivityIntegrationEvent(deliveryId, occurredOn.UtcDateTime,
                destination, eventId.ToString(), "Tickets", ticketId, owner, origin, actorUserId, activity),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
