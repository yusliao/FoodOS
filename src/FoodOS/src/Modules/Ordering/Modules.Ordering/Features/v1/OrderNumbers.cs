using FSH.Modules.Ordering.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1;

internal static class OrderNumbers
{
    public static async Task<string> NextAsync(
        OrderingDbContext dbContext,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        string prefix = $"SO{businessDate:yyyyMMdd}";
        int existing = await dbContext.SalesOrders
            .CountAsync(o => o.Number.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);
        return $"{prefix}{existing + 1:D4}";
    }
}
