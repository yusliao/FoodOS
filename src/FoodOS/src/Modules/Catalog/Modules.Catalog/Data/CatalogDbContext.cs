using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Catalog.Data;

public sealed class CatalogDbContext : BaseDbContext
{
    public const string Schema = "catalog";

    public CatalogDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<CatalogDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListLine> PriceListLines => Set<PriceListLine>();
    public DbSet<ProductContractLock> ProductContractLocks => Set<ProductContractLock>();
    public DbSet<CustomerLevel> CustomerLevels => Set<CustomerLevel>();
    public DbSet<CustomerLevelAssignment> CustomerLevelAssignments => Set<CustomerLevelAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        // Trigram index support for Product.SearchText (trusted extension; the database owner can create it).
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        // base.OnModelCreating runs LAST so BaseDbContext's auto-apply sees
        // fully-configured entities (including HasMany child types like ProductImage).
        base.OnModelCreating(modelBuilder);
    }
}
