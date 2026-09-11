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
        builder.Property(x => x.DeliveredQty).HasPrecision(18, 4);
        builder.Property(x => x.ReturnedQty).HasPrecision(18, 4);
        builder.Property(x => x.VarianceReason).HasMaxLength(256);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.HasMany(x => x.Lots)
            .WithOne()
            .HasForeignKey(l => l.SalesOrderLineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lots)
            .HasField("_lots")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}
