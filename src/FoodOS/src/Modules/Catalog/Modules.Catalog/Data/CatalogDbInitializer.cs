using FSH.Framework.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Catalog.Data;

public sealed class CatalogDbInitializer(
    CatalogDbContext dbContext,
    ILogger<CatalogDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Catalog] applied migrations");
        }
    }

    /// <summary>
    /// Catalog has NO per-tenant content seed. A fresh tenant comes up with an empty
    /// catalog and is expected to be populated by the operator via the API / UI.
    /// Demo content for the <c>acme</c> and <c>globex</c> tenants lives in the
    /// DbMigrator's <c>seed-demo</c> command, which calls
    /// <see cref="CatalogSeedData"/> directly under a tenant-scoped DbContext.
    /// The only work here is the search text backfill below.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Idempotent backfill: products created before Product.SearchText existed get it computed once.
        int updated = 0;
        while (true)
        {
            var batch = await dbContext.Products
                .Where(product => product.SearchText == string.Empty)
                .OrderBy(product => product.Id)
                .Take(500)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (batch.Count == 0) break;

            foreach (var product in batch)
            {
                product.RefreshSearchText();
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            updated += batch.Count;
        }

        if (updated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Catalog] backfilled search text for {Count} product(s)", updated);
        }
    }
}
