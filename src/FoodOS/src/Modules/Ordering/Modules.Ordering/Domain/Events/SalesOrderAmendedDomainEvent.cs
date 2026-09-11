using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain.Events;

public sealed record SalesOrderAmendedDomainEvent(
    Guid OrderId,
    int Revision,
    Guid EventId,
    DateTimeOffset OccurredOnUtc) : DomainEvent(EventId, OccurredOnUtc);
