using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain.Events;

public sealed record SalesOrderPlacedDomainEvent(
    Guid OrderId,
    string Number,
    Guid StoreId,
    Guid WarehouseId,
    Guid EventId,
    DateTimeOffset OccurredOnUtc) : DomainEvent(EventId, OccurredOnUtc);
