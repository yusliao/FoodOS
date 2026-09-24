namespace FSH.Modules.WmsIntegration.Contracts.v1;

public sealed record WmsAvailabilityRequest(
    string Sku,
    string Uom,
    decimal RequiredQuantity);

public sealed record WmsAvailabilityResult(
    string Sku,
    string Uom,
    decimal AvailableQuantity,
    bool IsAvailable,
    DateTimeOffset? AsOf);

public interface IWmsAvailabilityReader
{
    Task<IReadOnlyList<WmsAvailabilityResult>> GetAvailabilityAsync(
        string warehouseId,
        IReadOnlyCollection<WmsAvailabilityRequest> requests,
        CancellationToken cancellationToken = default);
}
