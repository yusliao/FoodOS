namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record SalesOrderLineDto(
    Guid Id,
    Guid ProductId,
    string Zone,
    decimal OrderedQty,
    decimal ReservedQty,
    decimal UnitPrice,
    string Currency,
    Guid? ReservationId);
