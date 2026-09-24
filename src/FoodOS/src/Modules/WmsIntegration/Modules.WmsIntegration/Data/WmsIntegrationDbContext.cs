using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsIntegrationDbContext : BaseDbContext
{
    public const string Schema = "wms";

    public WmsIntegrationDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<WmsIntegrationDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<WmsInboxMessage> InboxMessages => Set<WmsInboxMessage>();
    public DbSet<WmsObjectCursor> ObjectCursors => Set<WmsObjectCursor>();
    public DbSet<WmsMapping> Mappings => Set<WmsMapping>();
    public DbSet<WmsInventoryBalance> InventoryBalances => Set<WmsInventoryBalance>();
    public DbSet<WmsOutboundOperation> OutboundOperations => Set<WmsOutboundOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmsIntegrationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
