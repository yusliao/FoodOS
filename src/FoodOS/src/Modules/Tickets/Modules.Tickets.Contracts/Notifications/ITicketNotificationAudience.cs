namespace FSH.Modules.Tickets.Contracts.Notifications;

/// <summary>Resolves the current audience in the event delivery tenant, without exposing ticket content.</summary>
public interface ITicketNotificationAudience
{
    Task<TicketNotificationAudience?> ResolveAsync(
        Guid ticketId, string customerTenantId, TicketActivityKind activity, CancellationToken cancellationToken);
}

public sealed record TicketNotificationAudience(IReadOnlyList<Guid> ParticipantUserIds, bool NotifyOperatorQueue);
