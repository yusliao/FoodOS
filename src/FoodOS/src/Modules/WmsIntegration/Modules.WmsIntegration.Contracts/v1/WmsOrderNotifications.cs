namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record WmsOutboundOrderLine(
    Guid LineId,
    string Sku,
    string Uom,
    decimal Quantity);

public sealed record WmsOutboundOrderNotice(
    Guid OrderId,
    string OrderNumber,
    int Revision,
    string WarehouseId,
    string OwnerId,
    DateTimeOffset ShipBy,
    Guid StoreId,
    string StoreName,
    string StoreAddress,
    IReadOnlyList<WmsOutboundOrderLine> Lines);

public sealed record WmsOrderNotificationResult(
    string Status,
    string? ExternalOperationId,
    string? ErrorCode,
    string? Detail);

public interface IWmsOrderNotificationGateway
{
    Task<WmsOrderNotificationResult> SubmitAsync(
        string idempotencyKey,
        string correlationId,
        WmsOutboundOrderNotice notice,
        CancellationToken cancellationToken = default);

    Task<WmsOrderNotificationResult> CancelAsync(
        string idempotencyKey,
        string correlationId,
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default);
}
