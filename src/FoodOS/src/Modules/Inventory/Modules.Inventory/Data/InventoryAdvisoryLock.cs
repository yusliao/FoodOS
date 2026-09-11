using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Data;

internal static class InventoryAdvisoryLock
{
    public static Task AcquireSkuLockAsync(
        InventoryDbContext dbContext,
        Guid warehouseId,
        Guid productId,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        string key = $"inv:{warehouseId:N}:{productId:N}:{zoneId:N}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
            cancellationToken);
    }
}
