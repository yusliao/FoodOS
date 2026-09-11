using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain.Events;

public sealed record SalesOrderCancelledDomainEvent(
    Guid OrderId,
    Guid EventId,
    DateTimeOffset OccurredOnUtc) : DomainEvent(EventId, OccurredOnUtc);
