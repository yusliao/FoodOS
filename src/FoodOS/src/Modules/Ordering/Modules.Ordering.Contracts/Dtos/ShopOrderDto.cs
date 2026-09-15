namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopOrderLineDto(
    Guid Id,
    Guid ProductId,
    decimal OrderedQty,
    decimal DeliveredQty,
    decimal ReturnedQty,
    decimal ShortageQty,
    string? ShortageReason,
    decimal UnitPrice,
    string Currency);

public sealed record ShopOrderDto(
    Guid Id,
    string Number,
    Guid StoreId,
    string Status,
    DateOnly BusinessDate,
    DateTimeOffset CutoffAt,
    DateTimeOffset? PlacedAt,
    int Revision,
    IReadOnlyList<ShopOrderLineDto> Lines);
