using System.Globalization;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1;

internal static class WaveNumbers
{
    public static async Task<string> NextAsync(
        WarehouseDbContext dbContext,
        string warehouseCode,
        string zone,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        string prefix = $"WV{warehouseCode.Trim().ToUpperInvariant()}{zone.Trim().ToUpperInvariant()}{businessDate:yyyyMMdd}";
        int persisted = await dbContext.Waves
            .CountAsync(
                w => w.Number.StartsWith(prefix),
                cancellationToken)
            .ConfigureAwait(false);
        int pending = dbContext.ChangeTracker.Entries<Wave>()
            .Count(e =>
                e.State == EntityState.Added
                && e.Entity.Number.StartsWith(prefix, StringComparison.Ordinal));
        return $"{prefix}{(persisted + pending + 1).ToString("D2", CultureInfo.InvariantCulture)}";
    }
}
