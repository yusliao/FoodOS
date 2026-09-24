using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsReadiness(
    IOptions<WmsIntegrationOptions> options,
    WmsIntegrationDbContext db,
    IWmsStandardClient client) : IWmsReadiness
{
    public async Task<WmsReadinessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var reasons = new List<string>();
        if (!value.IsConfigured)
        {
            reasons.Add("notConfigured");
        }
        else
        {
            var required = new[]
            {
                (Kind: WmsMappingKinds.Warehouse, Value: value.WarehouseId, Reason: "warehouseMappingMissing"),
                (Kind: WmsMappingKinds.Owner, Value: value.Tenant, Reason: "ownerMappingMissing"),
            };
            var mappings = await db.Mappings.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Provider == value.Provider
                    && item.ConnectionId == value.ConnectionId
                    && item.IsActive)
                .Select(item => new { item.Kind, item.FoodOsValue })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var requirement in required)
            {
                if (!mappings.Exists(item => item.Kind == requirement.Kind
                    && string.Equals(item.FoodOsValue, requirement.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    reasons.Add(requirement.Reason);
                }
            }

            if (reasons.Count == 0)
            {
                using var healthTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                healthTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(value.RequestTimeoutSeconds, 1, 3)));
                bool isHealthy;
                try
                {
                    isHealthy = await client.IsHealthyAsync(healthTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    isHealthy = false;
                }

                if (!isHealthy)
                {
                    reasons.Add("wmsUnreachable");
                }
            }
        }

        return new WmsReadinessSnapshot(
            "externalWms",
            reasons.Count == 0 ? "ready" : reasons[0],
            value.IsConfigured,
            AcceptsOrders: true,
            AcceptsOrderChanges: true,
            LocalWarehouseExecution: false,
            value.IsConfigured ? value.Provider : null,
            value.IsConfigured ? value.ConnectionId : null,
            value.IsConfigured ? value.WarehouseId : null,
            reasons);
    }
}
