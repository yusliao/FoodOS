using FSH.Modules.WmsIntegration.Contracts.v1;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsReadiness(IOptions<WmsIntegrationOptions> options) : IWmsReadiness
{
    public WmsReadinessSnapshot GetSnapshot()
    {
        var value = options.Value;
        return new WmsReadinessSnapshot(
            "externalWms",
            value.IsConfigured ? "contractConfigured" : "notConfigured",
            value.IsConfigured,
            AcceptsOrders: false,
            AcceptsOrderChanges: false,
            LocalWarehouseExecution: false,
            value.IsConfigured ? value.Provider : null,
            value.IsConfigured ? value.ConnectionId : null,
            value.IsConfigured ? value.WarehouseId : null);
    }
}
