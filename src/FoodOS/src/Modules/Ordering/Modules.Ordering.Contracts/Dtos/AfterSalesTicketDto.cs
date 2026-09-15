namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record AfterSalesTicketDto(
    Guid Id,
    string? CustomerTenantId,
    Guid OrderId,
    Guid StoreId,
    Guid OrderLineId,
    string Type,
    decimal Quantity,
    string Reason,
    string Status,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);
