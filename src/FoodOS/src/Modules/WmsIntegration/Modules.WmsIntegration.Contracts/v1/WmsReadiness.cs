namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record WmsReadinessSnapshot(
    string Mode,
    string Readiness,
    bool ContractConfigured,
    bool AcceptsOrders,
    bool AcceptsOrderChanges,
    bool LocalWarehouseExecution,
    string? Provider,
    string? ConnectionId,
    string? WarehouseId,
    IReadOnlyList<string> BlockingReasons);

public interface IWmsReadiness
{
    Task<WmsReadinessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
