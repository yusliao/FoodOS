using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SalesOrderLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.OrderedQty).HasPrecision(18, 4);
        builder.Property(x => x.ReservedQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Ignore(x => x.DomainEvents);
    }
}
