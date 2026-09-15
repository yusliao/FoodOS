namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record SalesOrderDto(
    Guid Id,
    string Number,
    string? CustomerTenantId,
    Guid StoreId,
    Guid CustomerOrgId,
    Guid WarehouseId,
    string Status,
    DateOnly BusinessDate,
    DateTimeOffset CutoffAt,
    DateTimeOffset? PlacedAt,
    int Revision,
    IReadOnlyList<SalesOrderLineDto> Lines,
    Guid? RouteId = null);
