using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Tickets.Data;

public sealed class TicketsDbContext : BaseDbContext
{
    public const string Schema = "tickets";
    private readonly string? _requestTenantId;

    public TicketsDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<TicketsDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment)
    {
        ArgumentNullException.ThrowIfNull(multiTenantContextAccessor);
        _requestTenantId = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id;
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketsDbContext).Assembly);
        // base.OnModelCreating runs LAST so BaseDbContext's auto-apply sees
        // fully-configured entities (including HasMany child types).
        base.OnModelCreating(modelBuilder);
        // Cross-identity support is explicit: root can collaborate, customers stay in their domain.
        // Participant checks in handlers further restrict customer access within that domain.
        modelBuilder.Entity<Ticket>().HasQueryFilter("TicketAudience", ticket =>
            _requestTenantId != null && (_requestTenantId == MultitenancyConstants.Root.Id
                || ticket.CustomerTenantId == _requestTenantId));
        modelBuilder.Entity<TicketComment>().HasQueryFilter("TicketAudience", comment =>
            _requestTenantId != null && (_requestTenantId == MultitenancyConstants.Root.Id
                || comment.CustomerTenantId == _requestTenantId));
    }
}
