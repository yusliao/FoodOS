using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsAvailabilityReader(
    WmsIntegrationDbContext db,
    IOptions<WmsIntegrationOptions> options,
    TimeProvider clock) : IWmsAvailabilityReader
{
    public async Task<IReadOnlyList<WmsAvailabilityResult>> GetAvailabilityAsync(
        string warehouseId,
        IReadOnlyCollection<WmsAvailabilityRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouseId);
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return [];
        }

        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return requests.Select(Unavailable).ToList();
        }

        string normalizedWarehouse = Normalize(warehouseId);
        string normalizedOwner = Normalize(settings.Tenant);
        string normalizedProvider = Normalize(settings.Provider);
        string normalizedConnection = Normalize(settings.ConnectionId);
        var skus = requests.Select(x => Normalize(x.Sku)).Distinct().ToList();

        // WMS projections are owned by the configured operator tenant, while Shop requests run
        // inside restaurant tenants. Ignore the ambient tenant filter only after constraining the
        // configured provider, connection and owner explicitly.
        var balances = await db.InventoryBalances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Provider == normalizedProvider
                && x.ConnectionId == normalizedConnection
                && x.WarehouseId == normalizedWarehouse
                && x.OwnerId == normalizedOwner
                && skus.Contains(x.Sku))
            .GroupBy(x => new { x.Sku, x.Uom })
            .Select(group => new
            {
                group.Key.Sku,
                group.Key.Uom,
                AvailableQuantity = group.Sum(x => x.AvailableQuantity),
                AsOf = group.Max(x => x.OccurredAt),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byProduct = balances.ToDictionary(x => (x.Sku, x.Uom));
        DateTimeOffset freshAfter = clock.GetUtcNow().AddSeconds(
            -Math.Clamp(settings.InventoryProjectionMaxAgeSeconds, 1, 86400));
        return requests.Select(request =>
        {
            string sku = Normalize(request.Sku);
            string uom = Normalize(request.Uom);
            if (!byProduct.TryGetValue((sku, uom), out var balance))
            {
                return Unavailable(request);
            }

            return new WmsAvailabilityResult(
                request.Sku,
                request.Uom,
                balance.AvailableQuantity,
                balance.AsOf >= freshAfter && balance.AvailableQuantity >= request.RequiredQuantity,
                balance.AsOf);
        }).ToList();
    }

    private static WmsAvailabilityResult Unavailable(WmsAvailabilityRequest request) =>
        new(request.Sku, request.Uom, 0, false, null);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
