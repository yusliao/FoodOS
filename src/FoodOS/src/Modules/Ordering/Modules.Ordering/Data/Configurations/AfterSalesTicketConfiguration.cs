using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class AfterSalesTicketConfiguration : IEntityTypeConfiguration<AfterSalesTicket>
{
    public void Configure(EntityTypeBuilder<AfterSalesTicket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AfterSalesTickets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerTenantId).HasMaxLength(64);
        builder.HasIndex(x => new { x.CustomerTenantId, x.StoreId });
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(256);
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.StoreId);
        builder.Ignore(x => x.DomainEvents);
    }
}
