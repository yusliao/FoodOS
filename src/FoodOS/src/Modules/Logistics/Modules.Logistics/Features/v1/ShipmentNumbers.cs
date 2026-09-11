using System.Globalization;
using FSH.Modules.Logistics.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1;

internal static class ShipmentNumbers
{
    public static async Task<string> NextAsync(
        LogisticsDbContext dbContext,
        string routeCode,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        string prefix = $"SH{businessDate:yyyyMMdd}{routeCode.Trim().ToUpperInvariant()}";
        int existing = await dbContext.Shipments
            .CountAsync(s => s.Number.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);
        return $"{prefix}{(existing + 1).ToString("D2", CultureInfo.InvariantCulture)}";
    }
}
