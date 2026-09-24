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
    string? WarehouseId);

public interface IWmsReadiness
{
    WmsReadinessSnapshot GetSnapshot();
}
