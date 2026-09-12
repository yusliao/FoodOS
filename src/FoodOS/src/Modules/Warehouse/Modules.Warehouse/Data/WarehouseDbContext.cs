using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Warehouse.Data;

public sealed class WarehouseDbContext : BaseDbContext
{
    public const string Schema = "warehouse";

    public WarehouseDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<WarehouseDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Wave> Waves => Set<Wave>();
    public DbSet<PickTask> PickTasks => Set<PickTask>();
    public DbSet<TraceEvent> TraceEvents => Set<TraceEvent>();
    public DbSet<PutawayTask> PutawayTasks => Set<PutawayTask>();
    public DbSet<StockPlacement> StockPlacements => Set<StockPlacement>();
    public DbSet<PackTote> PackTotes => Set<PackTote>();
    public DbSet<PackToteOrder> PackToteOrders => Set<PackToteOrder>();
    public DbSet<Shrinkage> Shrinkages => Set<Shrinkage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WarehouseDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
