namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record WmsReservationLine(string LineId, string Sku, string Uom, decimal Quantity);

public sealed record WmsReserveOrderRequest(
    string WarehouseId,
    string OwnerId,
    IReadOnlyList<WmsReservationLine> Lines);

public sealed record WmsReservationResult(
    Guid OperationId,
    string Status,
    string? ReservationId,
    string? ErrorCode,
    string? Detail);

public interface IWmsReservationGateway
{
    Task<WmsReservationResult?> FindAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<WmsReservationResult> ReserveAsync(
        string idempotencyKey,
        string correlationId,
        WmsReserveOrderRequest request,
        CancellationToken cancellationToken = default);
}
