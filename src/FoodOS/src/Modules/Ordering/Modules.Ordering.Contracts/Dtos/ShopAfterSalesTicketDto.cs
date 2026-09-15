namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record ShopAfterSalesTicketDto(
    Guid Id,
    Guid OrderId,
    Guid StoreId,
    Guid OrderLineId,
    string Type,
    decimal Quantity,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt);
