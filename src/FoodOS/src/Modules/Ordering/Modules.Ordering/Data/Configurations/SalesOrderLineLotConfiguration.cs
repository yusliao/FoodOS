using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class SalesOrderLineLotConfiguration : IEntityTypeConfiguration<SalesOrderLineLot>
{
    public void Configure(EntityTypeBuilder<SalesOrderLineLot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SalesOrderLineLots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LotNo).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ShippedQty).HasPrecision(18, 4);
        builder.Property(x => x.DeliveredQty).HasPrecision(18, 4);
        builder.Property(x => x.ReturnedQty).HasPrecision(18, 4);
        builder.HasIndex(x => x.SalesOrderLineId);
        builder.HasIndex(x => x.LotId);
        builder.Ignore(x => x.DomainEvents);
    }
}
