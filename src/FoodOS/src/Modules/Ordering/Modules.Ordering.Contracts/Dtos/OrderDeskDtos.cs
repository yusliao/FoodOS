namespace FSH.Modules.Ordering.Contracts.Dtos;

/// <summary>A store that matches an incoming call (phone number, store/customer name or code).</summary>
public sealed record OrderDeskCallerDto(
    Guid StoreId,
    string StoreCode,
    string StoreName,
    string Address,
    string? DeliveryWindow,
    Guid CustomerOrgId,
    string CustomerOrgCode,
    string CustomerOrgName,
    bool CreditHold,
    bool CanPlaceOrders,
    string? BlockingReason,
    IReadOnlyList<StoreContactDto> Contacts,
    DateTimeOffset? LastOrderAt)
{
    /// <summary>Customer pricing level shown to customer service; null when the customer pays the standard price.</summary>
    public string? LevelCode { get; init; }
    public string? LevelName { get; init; }

    /// <summary>Percent off the standard price (10 = 9折).</summary>
    public decimal? LevelDiscountPercent { get; init; }
}

/// <summary>A store the current agent often orders for, with how many service orders they placed.</summary>
public sealed record OrderDeskFrequentCallerDto(OrderDeskCallerDto Caller, int OrderCount);

/// <summary>Type-ahead product candidate with the store's contract price and WMS availability.</summary>
public sealed record OrderDeskProductDto(
    Guid ProductId,
    string Sku,
    string Name,
    string BaseUom,
    bool CatchWeight,
    string? ThumbnailUrl,
    decimal UnitPrice,
    string Currency,
    string PriceSource,
    bool IsAvailable,
    string AvailabilityStatus);

public sealed record OrderDeskQuoteLineDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    string PriceSource,
    decimal LineTotal);

public sealed record OrderDeskOrderLineDto(
    Guid LineId,
    Guid ProductId,
    string Sku,
    string Name,
    string BaseUom,
    decimal OrderedQty,
    decimal ShortageQty,
    decimal UnitPrice,
    string Currency);

public sealed record OrderDeskOrderDto(
    Guid Id,
    string Number,
    string Status,
    DateOnly BusinessDate,
    DateTimeOffset? PlacedAt,
    DateTimeOffset CutoffAt,
    int Revision,
    string? PlacementChannel,
    string WarehouseConfirmationStatus,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderDeskOrderLineDto> Lines);

/// <summary>Products this store orders most often, for one-click re-ordering.</summary>
public sealed record OrderDeskFrequentProductDto(
    Guid ProductId,
    string Sku,
    string Name,
    string BaseUom,
    bool IsActive,
    int OrderCount,
    decimal TotalQty,
    decimal LastQty,
    DateTimeOffset LastOrderedAt);

public sealed record OrderDeskStoreHistoryDto(
    IReadOnlyList<OrderDeskOrderDto> RecentOrders,
    IReadOnlyList<OrderDeskFrequentProductDto> FrequentProducts);
