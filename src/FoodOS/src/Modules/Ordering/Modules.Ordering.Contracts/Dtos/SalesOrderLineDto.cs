namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record SalesOrderLineLotDto(
    Guid LotId,
    string LotNo,
    decimal ShippedQty,
    decimal DeliveredQty,
    decimal ReturnedQty);

public sealed record SalesOrderLineDto(
    Guid Id,
    Guid ProductId,
    string Zone,
    decimal OrderedQty,
    decimal ReservedQty,
    decimal DeliveredQty,
    decimal ReturnedQty,
    string? VarianceReason,
    decimal UnitPrice,
    string Currency,
    Guid? ReservationId,
    IReadOnlyList<SalesOrderLineLotDto> Lots);
