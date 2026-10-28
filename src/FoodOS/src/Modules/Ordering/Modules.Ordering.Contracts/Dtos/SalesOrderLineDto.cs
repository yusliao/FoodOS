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
    decimal ShortageQty,
    string? ShortageReason,
    string? VarianceReason,
    decimal UnitPrice,
    string Currency,
    Guid? ReservationId,
    IReadOnlyList<SalesOrderLineLotDto> Lots)
{
    /// <summary>Product display fields resolved for operator screens; null when not looked up.</summary>
    public string? Sku { get; init; }
    public string? ProductName { get; init; }
    public string? BaseUom { get; init; }
}
