using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Tickets.Contracts.Notifications;

/// <summary>TenantId is the delivery identity domain; origin and ticket ownership remain explicit.</summary>
public sealed record TicketActivityIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid TicketId,
    string CustomerTenantId,
    string OriginTenantId,
    Guid ActorUserId,
    TicketActivityKind Activity) : IIntegrationEvent;

public enum TicketActivityKind
{
    Created,
    Assigned,
    CommentAdded,
    StatusChanged,
}
