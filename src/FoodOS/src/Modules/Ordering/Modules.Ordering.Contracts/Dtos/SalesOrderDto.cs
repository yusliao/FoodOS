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
    Guid? RouteId = null,
    string? PlacementChannel = null)
{
    /// <summary>Display names resolved for operator screens; null when not looked up or no longer exists.</summary>
    public string? StoreCode { get; init; }
    public string? StoreName { get; init; }
    public string? CustomerOrgCode { get; init; }
    public string? CustomerOrgName { get; init; }
    public string? WarehouseCode { get; init; }
    public string? WarehouseName { get; init; }
}
