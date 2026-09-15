using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Ordering.Data;

public sealed class OrderingDbContext : BaseDbContext
{
    public const string Schema = "ordering";

    public OrderingDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<OrderingDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<CustomerOrg> CustomerOrgs => Set<CustomerOrg>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<CustomerUserStoreAccess> CustomerUserStoreAccesses => Set<CustomerUserStoreAccess>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartLine> CartLines => Set<CartLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<SalesOrderLineLot> SalesOrderLineLots => Set<SalesOrderLineLot>();
    public DbSet<AfterSalesTicket> AfterSalesTickets => Set<AfterSalesTicket>();
    public DbSet<ReconcileReminderLog> ReconcileReminderLogs => Set<ReconcileReminderLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
